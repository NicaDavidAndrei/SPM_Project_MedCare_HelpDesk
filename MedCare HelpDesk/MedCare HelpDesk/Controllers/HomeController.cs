using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MedCare_HelpDesk.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITicketService _ticketService;
        private readonly IKnowledgeBaseService _knowledgeBaseService;
        private readonly UserManager<User> _userManager;

        public HomeController(
            ITicketService ticketService,
            IKnowledgeBaseService knowledgeBaseService,
            UserManager<User> userManager)
        {
            _ticketService = ticketService;
            _knowledgeBaseService = knowledgeBaseService;
            _userManager = userManager;
        }

        public async Task<IActionResult> GuestDashboard()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("AdministratorDashboard");
                if (User.IsInRole("Agent")) return RedirectToAction("AgentDashboard");
                return RedirectToAction("UserDashboard");
            }

            ViewBag.KnowledgeCount = (await _knowledgeBaseService.GetAllArticlesAsync()).Count();
            return View();
        }

        [Authorize(Roles = "User")]
        public async Task<IActionResult> UserDashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                ViewBag.Notifications = await _ticketService.GetUnreadNotificationsAsync(currentUser.Id);
                var wardTickets = (await _ticketService.GetFilteredTicketsAsync(
                    currentUser.Id,
                    currentUser.DepartmentId,
                    null, null, null))
                    .Where(t => t.Status != "Closed")
                    .Take(5)
                    .ToList();
                ViewBag.WardTickets = wardTickets;
                ViewBag.WardName = (await _ticketService.GetFilteredTicketsAsync(null, currentUser.DepartmentId, null, null, null))
                    .FirstOrDefault()?.Department?.Name ?? "Assigned Ward";
            }

            ViewBag.QuickGuides = (await _knowledgeBaseService.GetAllArticlesAsync()).Take(3).ToList();
            return View();
        }

        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> AgentDashboard()
        {
            var ticketsList = (await _ticketService.GetAllTicketsAsync()).ToList();
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                ViewBag.MyWorkloadScore = await _ticketService.CalculateAgentWorkloadScoreAsync(currentUser.Id);
                ViewBag.CurrentUserId = currentUser.Id;
            }
            return View(ticketsList);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdministratorDashboard()
        {
            var tickets = (await _ticketService.GetAllTicketsAsync()).ToList();
            var currentUser = await _userManager.GetUserAsync(User);

            ViewBag.OpenTicketsCount = tickets.Count(t => string.Equals(t.Status, "Open", StringComparison.OrdinalIgnoreCase));
            ViewBag.InProgressCount = tickets.Count(t => string.Equals(t.Status, "In Progress", StringComparison.OrdinalIgnoreCase)
                                                      || string.Equals(t.Status, "Pending Info", StringComparison.OrdinalIgnoreCase));
            ViewBag.SlaBreachedCount = tickets.Count(t =>
            {
                var sla = _ticketService.EvaluateSlaHealth(t);
                return !sla.IsResolved && sla.IsBreached;
            });
            ViewBag.LateResolvedCount = tickets.Count(t =>
            {
                var sla = _ticketService.EvaluateSlaHealth(t);
                return sla.IsResolved && sla.IsBreached;
            });

            int evaluatedCount = tickets.Count;
            int compliantCount = tickets.Count(t => !_ticketService.EvaluateSlaHealth(t).IsBreached);
            ViewBag.SlaCompliancePct = evaluatedCount > 0 ? Math.Round((double)compliantCount / evaluatedCount * 100.0, 1) : 100.0;
            ViewBag.ActiveUsersCount = (await _ticketService.GetAllUsersAsync()).Count();

            if (currentUser != null)
            {
                ViewBag.MyWorkloadScore = await _ticketService.CalculateAgentWorkloadScoreAsync(currentUser.Id);
                ViewBag.MyActiveTicketsCount = tickets.Count(t => t.AssignedAgentId == currentUser.Id
                                                               && t.Status != "Resolved"
                                                               && t.Status != "Closed");
            }

            var staffWorkloads = await _ticketService.GetStaffWorkloadScoresAsync();
            ViewBag.TotalTeamWorkload = staffWorkloads.Values.Sum();

            return View(tickets);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationRead(int notificationId, int ticketId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _ticketService.MarkNotificationAsReadAsync(notificationId, user.Id);
            }
            return RedirectToAction("ViewTicket", "Tickets", new { id = ticketId });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
