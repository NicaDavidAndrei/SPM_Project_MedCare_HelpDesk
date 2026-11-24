using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MedCare_HelpDesk.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly ITicketService _ticketService;
        private readonly ICategoryService _categoryService;
        private readonly IDepartmentService _departmentService;
        private readonly UserManager<User> _userManager;
        private readonly IRecommendationService _recommendationService;
        private readonly ITicketPredictionService _predictionService;

        public TicketsController(
            ITicketService ticketService,
            ICategoryService categoryService,
            IDepartmentService departmentService,
            UserManager<User> userManager,
            IRecommendationService recommendationService,
            ITicketPredictionService predictionService)
        {
            _ticketService = ticketService;
            _categoryService = categoryService;
            _departmentService = departmentService;
            _userManager = userManager;
            _recommendationService = recommendationService;
            _predictionService = predictionService;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string? searchString, string? statusFilter, string? priorityFilter, int? departmentFilter, string? slaFilter)
        {
            var tickets = await _ticketService.GetFilteredTicketsAsync(null, null, searchString, statusFilter, priorityFilter, departmentFilter, slaFilter);
            SetViewDataFilters(searchString, statusFilter, priorityFilter, departmentFilter, slaFilter);
            return View(tickets);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var ticket = await _ticketService.GetTicketDetailsAsync(id.Value);
            return ticket == null ? NotFound() : View(ticket);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View(new Ticket { Status = "Open", Priority = "Medium" });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> Attachments)
        {
            ClearTicketModelState();

            if (Attachments != null && Attachments.Any())
            {
                foreach (var f in Attachments.Where(x => x.Length > 0))
                {
                    var (valid, err) = _ticketService.ValidateUploadFile(f);
                    if (!valid) ModelState.AddModelError(string.Empty, err!);
                }
            }

            if (ModelState.IsValid)
            {
                ticket.CreatedAt = DateTime.UtcNow;
                await _ticketService.CreateTicketAsync(ticket);
                await _ticketService.ProcessAttachmentsAsync(ticket.Id, Attachments ?? new List<IFormFile>());
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(ticket);
            return View(ticket);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var ticket = await _ticketService.GetTicketDetailsAsync(id.Value);
            if (ticket == null) return NotFound();

            await PopulateDropdownsAsync(ticket);
            return View(ticket);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TicketCode,Title,Description,EquipmentTag,Status,Priority,CreatedAt,DueDate,ResolvedAt,ResolutionSummary,CategoryId,DepartmentId,RequesterId,AssignedAgentId")] Ticket ticket)
        {
            if (id != ticket.Id) return NotFound();
            ClearTicketModelState();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _ticketService.GetTicketDetailsAsync(id);
                    if (existing != null && !string.Equals(existing.Priority, ticket.Priority, StringComparison.OrdinalIgnoreCase))
                    {
                        ticket.DueDate = _ticketService.CalculateDueDate(ticket.Priority, existing.CreatedAt);
                    }

                    bool isResolvedOrClosed = string.Equals(ticket.Status, "Resolved", StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(ticket.Status, "Closed", StringComparison.OrdinalIgnoreCase);
                    bool wasResolvedOrClosed = existing != null && (string.Equals(existing.Status, "Resolved", StringComparison.OrdinalIgnoreCase)
                                                                 || string.Equals(existing.Status, "Closed", StringComparison.OrdinalIgnoreCase));

                    if (isResolvedOrClosed)
                    {
                        ticket.ResolvedAt = (!wasResolvedOrClosed || existing?.ResolvedAt == null)
                            ? DateTime.UtcNow
                            : existing.ResolvedAt;
                    }
                    else
                    {
                        ticket.ResolvedAt = null;
                    }

                    await _ticketService.UpdateTicketAsync(ticket);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_ticketService.TicketExists(ticket.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(ticket);
            return View(ticket);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var ticket = await _ticketService.GetTicketDetailsAsync(id.Value);
            return ticket == null ? NotFound() : View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _ticketService.DeleteTicketAsync(id);
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// FR-14 & SEC-05: Medical Staff see tickets created by themselves OR assigned to their home ward (prevents shift duplicates).
        /// </summary>
        [Authorize(Roles = "Admin,Agent,User")]
        public async Task<IActionResult> MyTickets(string? searchString, string? statusFilter, string? priorityFilter)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var tickets = await _ticketService.GetFilteredTicketsAsync(
                currentUser.Id,
                currentUser.DepartmentId,
                searchString,
                statusFilter,
                priorityFilter);

            SetViewDataFilters(searchString, statusFilter, priorityFilter, null, null);
            return View(tickets);
        }

        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> AllTickets(string? searchString, string? statusFilter, string? priorityFilter, int? departmentFilter, string? slaFilter)
        {
            var tickets = (await _ticketService.GetFilteredTicketsAsync(
                null, null, searchString, statusFilter, priorityFilter, departmentFilter, slaFilter)).ToList();

            // Sort Breached active tickets first, then nearest SLA deadline, then newest (FR-12)
            var sorted = tickets
                .OrderByDescending(t =>
                {
                    var sla = _ticketService.EvaluateSlaHealth(t);
                    return !sla.IsResolved && sla.IsBreached ? 2 : (!sla.IsResolved && sla.IsApproaching ? 1 : 0);
                })
                .ThenBy(t => t.Status == "Resolved" || t.Status == "Closed" ? 1 : 0)
                .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                .ToList();

            SetViewDataFilters(searchString, statusFilter, priorityFilter, departmentFilter, slaFilter);
            return View(sorted);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Agent,User")]
        public async Task<IActionResult> SubmitTicket(int? categoryId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var vm = new SubmitTicketViewModel
            {
                DepartmentId = currentUser?.DepartmentId ?? 0,
                CategoryId = categoryId ?? 0
            };

            await PopulateSubmitDropdownsAsync(vm.DepartmentId, vm.CategoryId);
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Agent,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTicket(SubmitTicketViewModel model)
        {
            if (model.CategoryId == 0 && (!string.IsNullOrWhiteSpace(model.Title) || !string.IsNullOrWhiteSpace(model.Description)))
            {
                var prediction = _predictionService.PredictMetadata(model.Title ?? "", model.Description ?? "");
                if (!string.IsNullOrEmpty(prediction.PredictedCategory))
                {
                    var matchedCategory = _categoryService.GetAllCategories(activeOnly: true)
                        .FirstOrDefault(c => string.Equals(c.Name, prediction.PredictedCategory, StringComparison.OrdinalIgnoreCase));
                    if (matchedCategory != null)
                    {
                        model.CategoryId = matchedCategory.Id;
                        ModelState.Remove(nameof(model.CategoryId));
                    }
                }
            }

            var files = Request.HasFormContentType ? Request.Form.Files.ToList() : new List<IFormFile>();
            foreach (var f in files.Where(x => x.Length > 0))
            {
                var (isValid, err) = _ticketService.ValidateUploadFile(f);
                if (!isValid)
                {
                    ModelState.AddModelError(string.Empty, err!);
                }
            }

            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var ticket = new Ticket
                {
                    Title = (model.Title ?? string.Empty).Trim(),
                    Description = (model.Description ?? string.Empty).Trim(),
                    EquipmentTag = string.IsNullOrWhiteSpace(model.EquipmentTag) ? null : model.EquipmentTag.Trim(),
                    CategoryId = model.CategoryId,
                    DepartmentId = model.DepartmentId,
                    Priority = model.Priority,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    RequesterId = currentUser?.Id
                };

                await _ticketService.CreateTicketAsync(ticket, isUserSubmission: true);
                await _ticketService.ProcessAttachmentsAsync(ticket.Id, files);

                TempData["SuccessMessage"] = $"Ticket {ticket.TicketCode} submitted successfully ({ticket.Priority} SLA).";
                return RedirectToAction(nameof(MyTickets));
            }

            await PopulateSubmitDropdownsAsync(model.DepartmentId, model.CategoryId);
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Agent,User")]
        public async Task<JsonResult> GetKbSuggestions(string query)
        {
            var suggestions = await _recommendationService.GetRecommendedArticlesAsync(query, topK: 3);
            return Json(suggestions);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Agent,User")]
        public JsonResult PredictTicketFields(string title, string description)
        {
            var prediction = _predictionService.PredictMetadata(title, description);
            return Json(prediction);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<JsonResult> GetLeastLoadedAgent(int? categoryId)
        {
            var agent = await _ticketService.GetLeastLoadedAgentAsync(categoryId);
            if (agent == null) return Json(new { success = false });

            int workload = await _ticketService.CalculateAgentWorkloadScoreAsync(agent.Id);
            return Json(new
            {
                success = true,
                agentId = agent.Id,
                agentName = $"{agent.FirstName} {agent.LastName}",
                workload
            });
        }

        [Authorize(Roles = "Admin,Agent,User")]
        public async Task<IActionResult> ViewTicket(int id)
        {
            var ticket = await _ticketService.GetTicketDetailsAsync(id);
            if (ticket == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            bool isITStaff = User.IsInRole("Admin") || User.IsInRole("Agent");
            bool isSameWard = (ticket.DepartmentId.HasValue && ticket.DepartmentId.Value == currentUser.DepartmentId)
                           || (ticket.Requester?.DepartmentId == currentUser.DepartmentId);

            if (!isITStaff && ticket.RequesterId != currentUser.Id && !isSameWard)
            {
                return Forbid(); // HTTP 403 Anti-IDOR enforcement (SEC-04)
            }

            await _ticketService.MarkTicketNotificationsReadAsync(id, currentUser.Id);

            var supportStaff = await _ticketService.GetSupportStaffAsync();
            var workloads = await _ticketService.GetStaffWorkloadScoresAsync();

            ViewBag.SupportAgents = supportStaff
                .Where(u => string.Equals(u.Role, "Agent", StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => workloads.GetValueOrDefault(u.Id, 0))
                .Select(u =>
                {
                    int load = workloads.GetValueOrDefault(u.Id, 0);
                    string tier = load <= 3 ? "Low" : (load <= 6 ? "Medium" : "High");
                    return new { u.Id, Name = $"{u.FirstName} {u.LastName} (Load: {load} pts - {tier})" };
                }).ToList();

            ViewBag.Administrators = supportStaff
                .Where(u => string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => workloads.GetValueOrDefault(u.Id, 0))
                .Select(u =>
                {
                    int load = workloads.GetValueOrDefault(u.Id, 0);
                    string tier = load <= 3 ? "Low" : (load <= 6 ? "Medium" : "High");
                    return new { u.Id, Name = $"{u.FirstName} {u.LastName} (Load: {load} pts - {tier})" };
                }).ToList();

            if (ticket.AssignedAgentId.HasValue)
            {
                ViewBag.CurrentAgentLoad = workloads.GetValueOrDefault(ticket.AssignedAgentId.Value, 0);
            }

            int? requesterFilter = isITStaff ? null : currentUser.Id;
            int? wardFilter = isITStaff ? null : currentUser.DepartmentId;
            ViewBag.SimilarTickets = await _recommendationService.GetSimilarResolvedTicketsAsync(id, topK: 3, requesterFilter, wardFilter);

            return View(ticket);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Agent,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int ticketId, string text)
        {
            var files = Request.HasFormContentType ? Request.Form.Files.ToList() : new List<IFormFile>();

            if (string.IsNullOrWhiteSpace(text) && !files.Any())
                return Json(new { success = false, message = "Comment or attachment cannot be empty." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found." });

            var ticket = await _ticketService.GetTicketDetailsAsync(ticketId);
            if (ticket == null) return Json(new { success = false, message = "Ticket not found." });

            bool isITStaff = User.IsInRole("Admin") || User.IsInRole("Agent");
            bool isSameWard = (ticket.DepartmentId.HasValue && ticket.DepartmentId.Value == user.DepartmentId)
                           || (ticket.Requester?.DepartmentId == user.DepartmentId);

            if (!isITStaff && ticket.RequesterId != user.Id && !isSameWard)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { success = false, message = "Forbidden: Cross-ward ticket access denied." });
            }

            if (string.Equals(ticket.Status, "Resolved", StringComparison.OrdinalIgnoreCase)
             || string.Equals(ticket.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "This ticket is locked for new replies." });
            }

            foreach (var f in files.Where(x => x.Length > 0))
            {
                var (isValid, err) = _ticketService.ValidateUploadFile(f);
                if (!isValid) return Json(new { success = false, message = err });
            }

            var comment = new Comment
            {
                TicketId = ticketId,
                UserId = user.Id,
                Text = text?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow
            };

            await _ticketService.AddCommentAsync(comment);
            var (uploadedFilePaths, uploadErr) = await _ticketService.ProcessAttachmentsAsync(ticketId, files, comment.Id);
            if (!string.IsNullOrEmpty(uploadErr))
            {
                return Json(new { success = false, message = uploadErr });
            }

            if (ticket.RequesterId.HasValue && ticket.RequesterId.Value != user.Id)
            {
                string code = ticket.TicketCode ?? $"#{ticket.Id}";
                await _ticketService.CreateNotificationAsync(
                    ticket.RequesterId.Value,
                    ticket.Id,
                    $"New comment from {user.FirstName} {user.LastName} on {code}.");
            }

            string validProfilePic = "";
            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                string picPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(picPath)) validProfilePic = user.ProfilePicturePath;
            }

            return Json(new
            {
                success = true,
                userName = $"{user.FirstName} {user.LastName}".Trim(),
                date = comment.CreatedAt.ToString("MMM dd, yyyy, HH:mm") + " UTC",
                text = comment.Text,
                role = user.Role ?? "User",
                phone = user.PhoneNumber ?? "",
                profilePic = validProfilePic,
                attachments = uploadedFilePaths
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Agent")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClaimTicket(int ticketId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            bool ok = await _ticketService.ClaimTicketAsync(ticketId, currentUser.Id);
            if (!ok) return NotFound();

            TempData["SuccessMessage"] = "Ticket assigned to you and moved to In Progress.";
            return RedirectToAction(nameof(ViewTicket), new { id = ticketId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Agent")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTicketStatus(int TicketId, string Status, string Priority, int? AssignedAgentId, string? ResolutionSummary)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var (success, errorMsg) = await _ticketService.UpdateTicketWorkflowAsync(
                TicketId, Status, Priority, AssignedAgentId, ResolutionSummary, currentUser.Id);

            if (!success)
            {
                TempData["ErrorMessage"] = errorMsg ?? "Could not update ticket.";
            }
            else
            {
                TempData["SuccessMessage"] = "Ticket status and SLA tracking updated successfully.";
            }

            return RedirectToAction(nameof(ViewTicket), new { id = TicketId });
        }

        private void ClearTicketModelState()
        {
            ModelState.Remove("Category");
            ModelState.Remove("Department");
            ModelState.Remove("Requester");
            ModelState.Remove("AssignedAgent");
            ModelState.Remove("Comments");
            ModelState.Remove("Attachments");
            ModelState.Remove("Notifications");
        }

        private async Task PopulateDropdownsAsync(Ticket? ticket = null)
        {
            var supportStaff = await _ticketService.GetSupportStaffAsync();
            var allUsers = await _ticketService.GetAllUsersAsync();

            ViewData["AgentList"] = new SelectList(supportStaff.Select(u => new { u.Id, u.Email }), "Id", "Email", ticket?.AssignedAgentId);
            ViewData["RequesterList"] = new SelectList(allUsers.Select(u => new { u.Id, u.Email }), "Id", "Email", ticket?.RequesterId);
            ViewData["CategoryList"] = new SelectList(_categoryService.GetAllCategories(activeOnly: true), "Id", "Name", ticket?.CategoryId);
            ViewData["DepartmentList"] = new SelectList(_departmentService.GetAllDepartments(activeOnly: true), "Id", "Name", ticket?.DepartmentId);
        }

        private Task PopulateSubmitDropdownsAsync(int selectedDeptId, int selectedCatId)
        {
            var wards = _departmentService.GetAllDepartments(activeOnly: true).ToList();
            var categories = _categoryService.GetAllCategories(activeOnly: true).ToList();

            ViewBag.DepartmentList = new SelectList(wards, "Id", "Name", selectedDeptId);
            ViewBag.CriticalWardIds = wards.Where(w => w.IsCriticalCare).Select(w => w.Id).ToList();
            ViewBag.CategoryList = new SelectList(categories, "Id", "Name", selectedCatId);
            ViewBag.CategorySpecialties = categories.ToDictionary(c => c.Id, c => c.SpecialtyTag ?? "Support");
            return Task.CompletedTask;
        }

        private void SetViewDataFilters(string? search, string? status, string? priority, int? deptFilter, string? slaFilter)
        {
            ViewData["CurrentFilter"] = search;
            ViewData["StatusFilter"] = status;
            ViewData["PriorityFilter"] = priority;
            ViewData["DepartmentFilter"] = deptFilter;
            ViewData["SlaFilter"] = slaFilter;
            ViewBag.Departments = _departmentService.GetAllDepartments(activeOnly: true);
        }
    }
}
