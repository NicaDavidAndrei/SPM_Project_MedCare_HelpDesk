using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Repositories.Interfaces;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedCare_HelpDesk.Services
{
    public class TicketService : ITicketService
    {
        public const long MaxUploadSizeBytes = 5 * 1024 * 1024; // 5 MB (FR-18 / SEC-08)
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        private readonly IRepositoryWrapper _wrapper;
        private readonly HelpdeskContext _context;
        private readonly UserManager<User> _userManager;

        public TicketService(IRepositoryWrapper wrapper, HelpdeskContext context, UserManager<User> userManager)
        {
            _wrapper = wrapper;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IEnumerable<Ticket>> GetAllTicketsAsync() =>
            await _wrapper.Ticket.GetQueryableTicketsWithDetails()
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

        public async Task<Ticket?> GetTicketDetailsAsync(int id) =>
            await _wrapper.Ticket.GetTicketByIdAsync(id);

        public DateTime CalculateDueDate(string? priority, DateTime createdAt) =>
            CalculateDueDateStatic(priority, createdAt);

        /// <summary>
        /// 24/7 Hospital SLA deadline rules: Critical = 2h, High = 4h, Medium = 12h, Low = 24h.
        /// </summary>
        public static DateTime CalculateDueDateStatic(string? priority, DateTime createdAt) =>
            (priority ?? "").Trim().ToLowerInvariant() switch
            {
                "critical" => createdAt.AddHours(2),
                "high" => createdAt.AddHours(4),
                "medium" => createdAt.AddHours(12),
                "low" => createdAt.AddHours(24),
                _ => createdAt.AddHours(12)
            };

        public SlaHealthInfo EvaluateSlaHealth(Ticket ticket, DateTime? utcNow = null) =>
            EvaluateSlaHealthStatic(ticket, utcNow);

        public static SlaHealthInfo EvaluateSlaHealthStatic(Ticket ticket, DateTime? utcNow = null)
        {
            DateTime now = utcNow ?? DateTime.UtcNow;
            bool isResolved = string.Equals(ticket.Status, "Resolved", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(ticket.Status, "Closed", StringComparison.OrdinalIgnoreCase);

            if (!ticket.DueDate.HasValue)
            {
                return new SlaHealthInfo(isResolved, false, false, 1.0, null);
            }

            if (isResolved)
            {
                bool late = ticket.ResolvedAt.HasValue && ticket.ResolvedAt.Value > ticket.DueDate.Value;
                return new SlaHealthInfo(true, late, false, late ? 0.0 : 1.0, TimeSpan.Zero);
            }

            TimeSpan total = ticket.DueDate.Value - ticket.CreatedAt;
            TimeSpan remaining = ticket.DueDate.Value - now;
            double ratio = total.TotalSeconds > 0 ? remaining.TotalSeconds / total.TotalSeconds : 0.0;

            bool isBreached = now > ticket.DueDate.Value;
            bool isApproaching = !isBreached && ratio <= 0.25;
            return new SlaHealthInfo(false, isBreached, isApproaching, Math.Clamp(ratio, 0.0, 1.0), remaining);
        }

        public async Task CreateTicketAsync(Ticket ticket, bool isUserSubmission = false)
        {
            var baseTime = ticket.CreatedAt == default ? DateTime.UtcNow : ticket.CreatedAt;
            ticket.CreatedAt = baseTime;
            ticket.DueDate = CalculateDueDateStatic(ticket.Priority, baseTime);

            if (!ticket.DepartmentId.HasValue && ticket.RequesterId.HasValue)
            {
                var requester = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == ticket.RequesterId.Value);
                if (requester != null)
                {
                    ticket.DepartmentId = requester.DepartmentId;
                }
            }

            if (string.IsNullOrWhiteSpace(ticket.TicketCode))
            {
                int nextSeq = (await _context.Tickets.MaxAsync(t => (int?)t.Id) ?? 0) + 1;
                string candidate;
                do
                {
                    candidate = $"TKT-{baseTime:yyyy}-{nextSeq:D4}";
                    nextSeq++;
                } while (await _context.Tickets.AnyAsync(t => t.TicketCode == candidate));
                ticket.TicketCode = candidate;
            }

            // Smart Agent Routing: auto-assign ticket to least-loaded active agent if unassigned
            if (ticket.AssignedAgentId == null || ticket.AssignedAgentId == 0)
            {
                var leastLoadedAgent = await GetLeastLoadedAgentAsync(ticket.CategoryId);
                if (leastLoadedAgent != null)
                {
                    ticket.AssignedAgentId = leastLoadedAgent.Id;
                }
            }

            _wrapper.Ticket.Create(ticket);
            await _wrapper.SaveAsync();
        }

        public async Task UpdateTicketAsync(Ticket ticket)
        {
            _wrapper.Ticket.Update(ticket);
            await _wrapper.SaveAsync();
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateTicketWorkflowAsync(
            int ticketId,
            string status,
            string priority,
            int? assignedAgentId,
            string? resolutionSummary,
            int actorUserId)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null) return (false, "Ticket not found.");

            bool isResolving = string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase);
            bool isResolvedOrClosed = isResolving || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);
            bool wasResolvedOrClosed = string.Equals(ticket.Status, "Resolved", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(ticket.Status, "Closed", StringComparison.OrdinalIgnoreCase);

            if (isResolvedOrClosed && string.IsNullOrWhiteSpace(resolutionSummary) && string.IsNullOrWhiteSpace(ticket.ResolutionSummary))
            {
                return (false, "A technical resolution summary note is required when resolving or closing a ticket (FR-17).");
            }

            bool priorityChanged = !string.Equals(ticket.Priority, priority, StringComparison.OrdinalIgnoreCase);
            bool statusChanged = !string.Equals(ticket.Status, status, StringComparison.OrdinalIgnoreCase);

            ticket.Status = status;
            ticket.Priority = priority;
            ticket.AssignedAgentId = assignedAgentId;

            if (!string.IsNullOrWhiteSpace(resolutionSummary))
            {
                ticket.ResolutionSummary = resolutionSummary.Trim();
            }

            if (isResolvedOrClosed)
            {
                if (!wasResolvedOrClosed || !ticket.ResolvedAt.HasValue)
                {
                    ticket.ResolvedAt = DateTime.UtcNow;
                }
            }
            else
            {
                ticket.ResolvedAt = null;
            }

            if (priorityChanged || !ticket.DueDate.HasValue)
            {
                ticket.DueDate = CalculateDueDateStatic(priority, ticket.CreatedAt);
            }

            await _context.SaveChangesAsync();

            if (ticket.RequesterId.HasValue && ticket.RequesterId.Value != actorUserId && (statusChanged || priorityChanged))
            {
                string code = ticket.TicketCode ?? $"#{ticket.Id}";
                await CreateNotificationAsync(
                    ticket.RequesterId.Value,
                    ticket.Id,
                    $"Ticket {code} ({ticket.Title}) updated to '{ticket.Status}' ({ticket.Priority} priority).");
            }

            return (true, null);
        }

        public async Task<bool> ClaimTicketAsync(int ticketId, int agentId)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null) return false;

            var agent = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == agentId);
            ticket.AssignedAgentId = agentId;
            if (string.Equals(ticket.Status, "Open", StringComparison.OrdinalIgnoreCase))
            {
                ticket.Status = "In Progress";
            }

            await _context.SaveChangesAsync();

            if (ticket.RequesterId.HasValue && ticket.RequesterId.Value != agentId)
            {
                string agentName = agent != null ? $"{agent.FirstName} {agent.LastName}".Trim() : "Support Agent";
                string code = ticket.TicketCode ?? $"#{ticket.Id}";
                await CreateNotificationAsync(
                    ticket.RequesterId.Value,
                    ticket.Id,
                    $"Ticket {code} was claimed by {agentName} and is now In Progress.");
            }

            return true;
        }

        public async Task DeleteTicketAsync(int id)
        {
            _context.ChangeTracker.Clear();

            var ticket = await _context.Tickets
                .Include(t => t.Comments)
                    .ThenInclude(c => c.Attachments)
                .Include(t => t.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket != null)
            {
                var filesToDelete = ticket.Attachments
                    .Concat(ticket.Comments.SelectMany(c => c.Attachments ?? Enumerable.Empty<Attachment>()))
                    .ToList();
                foreach (var att in filesToDelete)
                {
                    string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", att.FilePath.TrimStart('/'));
                    if (File.Exists(path)) File.Delete(path);
                }

                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();
            }
        }

        public bool TicketExists(int id) => _wrapper.Ticket.TicketExists(id);

        public async Task<IEnumerable<Ticket>> GetFilteredTicketsAsync(
            int? requesterId,
            int? wardId,
            string? searchString,
            string? statusFilter,
            string? priorityFilter,
            int? departmentFilter = null,
            string? slaFilter = null)
        {
            var query = _wrapper.Ticket.GetQueryableTicketsWithDetails();

            if (requesterId.HasValue && wardId.HasValue)
            {
                query = query.Where(t =>
                    t.RequesterId == requesterId.Value ||
                    t.DepartmentId == wardId.Value ||
                    (t.Requester != null && t.Requester.DepartmentId == wardId.Value));
            }
            else if (requesterId.HasValue)
            {
                query = query.Where(t => t.RequesterId == requesterId.Value);
            }
            else if (wardId.HasValue)
            {
                query = query.Where(t => t.DepartmentId == wardId.Value || (t.Requester != null && t.Requester.DepartmentId == wardId.Value));
            }

            if (departmentFilter.HasValue && departmentFilter.Value > 0)
            {
                query = query.Where(t => t.DepartmentId == departmentFilter.Value || (t.Requester != null && t.Requester.DepartmentId == departmentFilter.Value));
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string term = searchString.Trim();
                string termLower = term.ToLower();
                if (int.TryParse(term, out int searchId))
                {
                    query = query.Where(t => t.Id == searchId
                                          || (t.TicketCode != null && t.TicketCode.ToLower().Contains(termLower))
                                          || (t.Title != null && t.Title.ToLower().Contains(termLower))
                                          || (t.Description != null && t.Description.ToLower().Contains(termLower))
                                          || (t.EquipmentTag != null && t.EquipmentTag.ToLower().Contains(termLower)));
                }
                else
                {
                    query = query.Where(t => (t.TicketCode != null && t.TicketCode.ToLower().Contains(termLower))
                                          || (t.Title != null && t.Title.ToLower().Contains(termLower))
                                          || (t.Description != null && t.Description.ToLower().Contains(termLower))
                                          || (t.EquipmentTag != null && t.EquipmentTag.ToLower().Contains(termLower)));
                }
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                query = query.Where(t => t.Status == statusFilter);

            if (!string.IsNullOrWhiteSpace(priorityFilter) && priorityFilter != "All")
                query = query.Where(t => t.Priority == priorityFilter);

            var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            if (!string.IsNullOrWhiteSpace(slaFilter) && slaFilter != "All")
            {
                DateTime now = DateTime.UtcNow;
                list = slaFilter switch
                {
                    "Breached" => list.Where(t => { var s = EvaluateSlaHealthStatic(t, now); return !s.IsResolved && s.IsBreached; }).ToList(),
                    "Approaching" => list.Where(t => { var s = EvaluateSlaHealthStatic(t, now); return !s.IsResolved && s.IsApproaching; }).ToList(),
                    "OnTrack" => list.Where(t => { var s = EvaluateSlaHealthStatic(t, now); return !s.IsResolved && !s.IsBreached && !s.IsApproaching; }).ToList(),
                    "Met" => list.Where(t => { var s = EvaluateSlaHealthStatic(t, now); return s.IsResolved && !s.IsBreached; }).ToList(),
                    _ => list
                };
            }

            return list;
        }

        public async Task AddCommentAsync(Comment comment)
        {
            _wrapper.Comment.Create(comment);
            await _wrapper.SaveAsync();
        }

        public async Task AddAttachmentAsync(Attachment attachment)
        {
            _wrapper.Attachment.Create(attachment);
            await _wrapper.SaveAsync();
        }

        public (bool IsValid, string? ErrorMessage) ValidateUploadFile(IFormFile file, bool imagesOnly = false) =>
            ValidateUploadFileStatic(file, imagesOnly);

        public static (bool IsValid, string? ErrorMessage) ValidateUploadFileStatic(IFormFile file, bool imagesOnly = false)
        {
            if (file == null || file.Length <= 0)
                return (false, "Empty file cannot be uploaded.");

            if (file.Length > MaxUploadSizeBytes)
                return (false, $"File '{Path.GetFileName(file.FileName)}' exceeds the 5 MB size limit.");

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (imagesOnly)
            {
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                    return (false, $"File '{Path.GetFileName(file.FileName)}' must be a .jpg, .jpeg, or .png image.");
            }
            else if (!AllowedExtensions.Contains(ext))
            {
                return (false, $"File type '{ext}' is not allowed. Accepted types: .jpg, .jpeg, .png, .pdf.");
            }

            // Magic-byte verification to block renamed executables (SEC-08)
            Span<byte> header = stackalloc byte[8];
            using var stream = file.OpenReadStream();
            int read = stream.Read(header);
            if (read < 4)
                return (false, $"File '{Path.GetFileName(file.FileName)}' is corrupted or too small.");

            bool magicValid = ext switch
            {
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                _ => false
            };

            return magicValid
                ? (true, null)
                : (false, $"File '{Path.GetFileName(file.FileName)}' content does not match its '{ext}' extension.");
        }

        public async Task<(List<string> UploadedPaths, string? ErrorMessage)> ProcessAttachmentsAsync(
            int ticketId,
            IEnumerable<IFormFile> files,
            int? commentId = null)
        {
            var uploadedFilePaths = new List<string>();
            var fileList = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
            if (!fileList.Any()) return (uploadedFilePaths, null);

            foreach (var file in fileList)
            {
                var (isValid, err) = ValidateUploadFileStatic(file);
                if (!isValid) return (uploadedFilePaths, err);
            }

            string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tickets");
            if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

            foreach (var file in fileList)
            {
                string safeName = Path.GetFileName(file.FileName);
                string uniqueFileName = Guid.NewGuid() + "_" + safeName;
                string filePath = Path.Combine(uploadFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                var attachment = new Attachment
                {
                    FileName = safeName,
                    FilePath = "/uploads/tickets/" + uniqueFileName,
                    FileSizeBytes = file.Length,
                    TicketId = ticketId,
                    CommentId = commentId,
                    CreatedAt = DateTime.UtcNow
                };

                _wrapper.Attachment.Create(attachment);
                uploadedFilePaths.Add(attachment.FilePath);
            }

            await _wrapper.SaveAsync();
            return (uploadedFilePaths, null);
        }

        public async Task<IEnumerable<User>> GetSupportStaffAsync()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var agents = await _userManager.GetUsersInRoleAsync("Agent");
            var staffIds = admins.Concat(agents).Select(u => u.Id).Distinct().ToList();

            return await _context.Users
                .Include(u => u.Department)
                .Where(u => staffIds.Contains(u.Id) && u.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync() =>
            await _userManager.Users.Where(u => u.IsActive).ToListAsync();

        public async Task<int> CalculateAgentWorkloadScoreAsync(int agentId)
        {
            var activeTickets = await _context.Tickets
                .Where(t => t.AssignedAgentId == agentId &&
                            t.Status != null &&
                            t.Status.ToLower() != "resolved" &&
                            t.Status.ToLower() != "closed")
                .Select(t => t.Priority)
                .ToListAsync();

            return activeTickets.Sum(GetPriorityWeight);
        }

        public static int GetPriorityWeight(string? priority) =>
            (priority ?? "").Trim().ToLowerInvariant() switch
            {
                "critical" => 4,
                "high" => 3,
                "medium" => 2,
                "low" => 1,
                _ => 1
            };

        public async Task<Dictionary<int, int>> GetStaffWorkloadScoresAsync()
        {
            var staff = await GetSupportStaffAsync();
            var scores = new Dictionary<int, int>();

            var activeTickets = await _context.Tickets
                .Where(t => t.AssignedAgentId.HasValue &&
                            t.Status != null &&
                            t.Status.ToLower() != "resolved" &&
                            t.Status.ToLower() != "closed")
                .Select(t => new { AgentId = t.AssignedAgentId!.Value, Priority = t.Priority ?? "" })
                .ToListAsync();

            var groupedScores = activeTickets
                .GroupBy(t => t.AgentId)
                .ToDictionary(g => g.Key, g => g.Sum(t => GetPriorityWeight(t.Priority)));

            foreach (var member in staff)
            {
                scores[member.Id] = groupedScores.GetValueOrDefault(member.Id, 0);
            }

            return scores;
        }

        public async Task<User?> GetLeastLoadedAgentAsync(int? categoryId = null)
        {
            var allStaff = (await GetSupportStaffAsync()).ToList();

            var agents = allStaff.Where(u => string.Equals(u.Role, "Agent", StringComparison.OrdinalIgnoreCase)).ToList();
            if (!agents.Any()) agents = allStaff;
            if (!agents.Any()) return null;

            var workloads = await GetStaffWorkloadScoresAsync();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                var category = await _context.Categories.FindAsync(categoryId.Value);
                if (category != null)
                {
                    string catKey = category.Name ?? "";
                    string specKey = category.SpecialtyTag ?? "";
                    var matchingStaff = agents.Where(u =>
                        u.Department != null && u.Department.Name != null &&
                        ((!string.IsNullOrEmpty(catKey) && (u.Department.Name.Contains(catKey, StringComparison.OrdinalIgnoreCase) || catKey.Contains(u.Department.Name, StringComparison.OrdinalIgnoreCase))) ||
                         (!string.IsNullOrEmpty(specKey) && (u.Department.Name.Contains(specKey, StringComparison.OrdinalIgnoreCase) || specKey.Contains(u.Department.Name, StringComparison.OrdinalIgnoreCase))))
                    ).ToList();

                    if (matchingStaff.Any())
                    {
                        return matchingStaff.OrderBy(u => workloads.GetValueOrDefault(u.Id, 0)).First();
                    }
                }
            }

            return agents.OrderBy(u => workloads.GetValueOrDefault(u.Id, 0)).First();
        }

        public async Task CreateNotificationAsync(int userId, int ticketId, string message)
        {
            var notification = new TicketNotification
            {
                UserId = userId,
                TicketId = ticketId,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _wrapper.Notification.Create(notification);
            await _wrapper.SaveAsync();
        }

        public async Task<List<TicketNotification>> GetUnreadNotificationsAsync(int userId) =>
            await _context.TicketNotifications
                .Include(n => n.Ticket)
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

        public async Task MarkNotificationAsReadAsync(int notificationId, int userId)
        {
            var notif = await _context.TicketNotifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkTicketNotificationsReadAsync(int ticketId, int userId)
        {
            var notifs = await _context.TicketNotifications
                .Where(n => n.TicketId == ticketId && n.UserId == userId && !n.IsRead)
                .ToListAsync();
            if (notifs.Any())
            {
                foreach (var n in notifs) n.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}
