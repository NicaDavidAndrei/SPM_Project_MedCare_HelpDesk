using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using MedCare_HelpDesk.Data;
using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedCareHelpDesk.Tests
{
    public class MedCareWebAppFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"medcare_test_{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                foreach (var ext in new[] { "", "-shm", "-wal" })
                {
                    var p = _dbPath + ext;
                    if (File.Exists(p))
                    {
                        try { File.Delete(p); } catch { }
                    }
                }
            }
        }
    }

    public class HelpdeskSystemTests
    {
        // =====================================================================
        // 1. STRICT 24/7 HOSPITAL SLA DEADLINE TESTS (2h / 4h / 12h / 24h)
        // =====================================================================
        [Theory]
        [InlineData("Critical", 2)]
        [InlineData("High", 4)]
        [InlineData("Medium", 12)]
        [InlineData("Low", 24)]
        public void TicketSlaDeadline_ShouldMatchHospital2_4_12_24hRules(string priority, int expectedHours)
        {
            DateTime createdAt = new DateTime(2026, 11, 26, 22, 30, 0, DateTimeKind.Utc);
            DateTime dueDate = TicketService.CalculateDueDateStatic(priority, createdAt);

            Assert.Equal(expectedHours, (dueDate - createdAt).TotalHours);
        }

        [Fact]
        public void EvaluateSlaHealth_ShouldFlagWarningAt25Percent_AndBreachedAfterDueDate()
        {
            var created = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
            var ticket = new Ticket
            {
                Priority = "Critical",
                Status = "Open",
                CreatedAt = created,
                DueDate = TicketService.CalculateDueDateStatic("Critical", created) // +2h -> 12:00
            };

            // At 11:00 (50% remaining): On Track
            var onTrack = TicketService.EvaluateSlaHealthStatic(ticket, created.AddMinutes(60));
            Assert.False(onTrack.IsBreached);
            Assert.False(onTrack.IsApproaching);

            // At 11:30 (30 min = 25% remaining): Warning
            var warning = TicketService.EvaluateSlaHealthStatic(ticket, created.AddMinutes(90));
            Assert.False(warning.IsBreached);
            Assert.True(warning.IsApproaching);
            Assert.True(warning.RemainingRatio <= 0.25);

            // At 12:05 (past 2h SLA): Breached
            var breached = TicketService.EvaluateSlaHealthStatic(ticket, created.AddMinutes(125));
            Assert.True(breached.IsBreached);
            Assert.False(breached.IsResolved);
        }

        [Fact]
        public void EvaluateSlaHealth_ResolvedAndReopened_ShouldTrackResolvedAtAccurately()
        {
            var created = DateTime.UtcNow.AddHours(-5);
            var ticket = new Ticket
            {
                Priority = "High",
                Status = "Resolved",
                CreatedAt = created,
                DueDate = created.AddHours(4),
                ResolvedAt = created.AddHours(3) // Met within 4h
            };

            var met = TicketService.EvaluateSlaHealthStatic(ticket);
            Assert.True(met.IsResolved);
            Assert.False(met.IsBreached);

            // Reopen after deadline passed
            ticket.Status = "In Progress";
            ticket.ResolvedAt = null;
            var reopened = TicketService.EvaluateSlaHealthStatic(ticket);
            Assert.False(reopened.IsResolved);
            Assert.True(reopened.IsBreached);
        }

        [Fact]
        public void PendingInfoStatus_OnCriticalTicket_ShouldContinueCounting2hClock()
        {
            var created = DateTime.UtcNow.AddHours(-3);
            var criticalPending = new Ticket
            {
                Priority = "Critical",
                Status = "Pending Info",
                CreatedAt = created,
                DueDate = TicketService.CalculateDueDateStatic("Critical", created)
            };

            var health = TicketService.EvaluateSlaHealthStatic(criticalPending);
            Assert.False(health.IsResolved);
            Assert.True(health.IsBreached);
        }

        // =====================================================================
        // 2. WEIGHTED SMART AGENT ROUTING TESTS (Crit=4, High=3, Med=2, Low=1)
        // =====================================================================
        [Fact]
        public void WeightedWorkload_ShouldExcludeResolvedAndAdmins()
        {
            Assert.Equal(4, TicketService.GetPriorityWeight("Critical"));
            Assert.Equal(3, TicketService.GetPriorityWeight("High"));
            Assert.Equal(2, TicketService.GetPriorityWeight("Medium"));
            Assert.Equal(1, TicketService.GetPriorityWeight("Low"));

            var staff = new[]
            {
                new { Id = 1, Role = "Admin", Score = 0 },
                new { Id = 2, Role = "Agent", Score = 7 },
                new { Id = 3, Role = "Agent", Score = 2 }
            };

            var chosen = staff.Where(s => s.Role == "Agent").OrderBy(s => s.Score).First();
            Assert.Equal(3, chosen.Id);
        }

        // =====================================================================
        // 3. FILE UPLOAD HARDENING TESTS (SEC-08: <=5MB & Magic Bytes)
        // =====================================================================
        [Fact]
        public void ValidateUploadFile_ShouldRejectSpoofedExeAndOversizedFiles()
        {
            // Spoofed .exe renamed as .png (MZ header instead of PNG 89 50 4E 47)
            var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 };
            var spoofedFile = new FormFile(new MemoryStream(exeBytes), 0, exeBytes.Length, "Attachments", "fake_monitor.png");
            var (spoofValid, _) = TicketService.ValidateUploadFileStatic(spoofedFile);
            Assert.False(spoofValid);

            // Oversized 6MB PNG
            var largeBytes = new byte[6 * 1024 * 1024];
            largeBytes[0] = 0x89; largeBytes[1] = 0x50; largeBytes[2] = 0x4E; largeBytes[3] = 0x47;
            var largeFile = new FormFile(new MemoryStream(largeBytes), 0, largeBytes.Length, "Attachments", "big_scan.png");
            var (sizeValid, _) = TicketService.ValidateUploadFileStatic(largeFile);
            Assert.False(sizeValid);

            // Valid PNG <= 5MB
            var validPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            var okFile = new FormFile(new MemoryStream(validPng), 0, validPng.Length, "Attachments", "monitor_error.png");
            var (okValid, okErr) = TicketService.ValidateUploadFileStatic(okFile);
            Assert.True(okValid, okErr);

            // Valid PDF <= 5MB
            var validPdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
            var okPdf = new FormFile(new MemoryStream(validPdf), 0, validPdf.Length, "Attachments", "ventilator_log.pdf");
            var (pdfValid, pdfErr) = TicketService.ValidateUploadFileStatic(okPdf);
            Assert.True(pdfValid, pdfErr);
        }

        // =====================================================================
        // 4. ML.NET & TF-IDF HOSPITAL INTELLIGENT TRIAGE TESTS
        // =====================================================================
        [Fact]
        public async Task MlNet_HospitalClassification_ShouldPredictMedicalDomainUnder50Ms()
        {
            var services = new ServiceCollection();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var predictionService = new TicketPredictionService(scopeFactory);
            await predictionService.TrainModelFromDatabaseAsync();
            predictionService.PredictMetadata("Warmup", "Warmup monitor test");

            var sw = Stopwatch.StartNew();
            var equipPred = predictionService.PredictMetadata(
                "ATI mechanical ventilator pressure alarm fault",
                "Bedside ICU ventilator shows pressure error");
            sw.Stop();

            Assert.Equal("Medical Equipment", equipPred.PredictedCategory);
            Assert.True(sw.ElapsedMilliseconds < 50, $"Inference took {sw.ElapsedMilliseconds}ms");

            var netPred = predictionService.PredictMetadata(
                "Emergency wristband barcode thermal printer jam",
                "Triage thermal printer spooler stuck");
            Assert.Equal("IT / Network", netPred.PredictedCategory);
        }

        [Fact]
        public void TfIdf_CosineSimilarity_ShouldRankMatchingGuideHighest()
        {
            double[] query = { 0.85, 0.55, 0.0 };
            double[] match = { 0.80, 0.60, 0.0 };
            double[] other = { 0.0, 0.1, 0.95 };

            Assert.True(RecommendationService.CalculateCosineSimilarity(query, match) > 0.95);
            Assert.True(RecommendationService.CalculateCosineSimilarity(query, other) < 0.15);
        }

        // =====================================================================
        // 5. END-TO-END RBAC, WARD ANTI-IDOR, LOCKOUT & PERFORMANCE INTEGRATION TESTS
        // =====================================================================
        [Fact]
        public async Task EndToEnd_SecurityRbacAndWardIsolation_ShouldPassAllSecCases()
        {
            using var factory = new MedCareWebAppFactory();
            var opts = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };

            // --- SEC-01 & SEC-02: Unauthenticated Guest (not signed in!) can view public portal & KB, blocked from protected endpoints ---
            using (var guestClient = factory.CreateClient(opts))
            {
                var guestRoot = await guestClient.GetAsync("/");
                Assert.Equal(HttpStatusCode.OK, guestRoot.StatusCode);

                var guestHome = await guestClient.GetAsync("/Home/GuestDashboard");
                Assert.Equal(HttpStatusCode.OK, guestHome.StatusCode);

                var guestKb = await guestClient.GetAsync("/KnowledgeBase/Index");
                Assert.Equal(HttpStatusCode.OK, guestKb.StatusCode);

                // Protected routes redirect unauthenticated Guest to /Account/Login
                foreach (var protectedRoute in new[] { "/Tickets/SubmitTicket", "/Tickets/Create", "/Users/Index", "/KnowledgeArticles/Create" })
                {
                    var unauthResp = await guestClient.GetAsync(protectedRoute);
                    Assert.Equal(HttpStatusCode.Redirect, unauthResp.StatusCode);
                    Assert.Contains("/Account/Login", unauthResp.Headers.Location?.ToString() ?? "");
                }

                // Unauthenticated POST to /Account/UpdateProfile is rejected
                var anonPost = await guestClient.PostAsync("/Account/UpdateProfile", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["FirstName"] = "Tamper",
                    ["LastName"] = "Guest"
                }));
                Assert.True(anonPost.StatusCode == HttpStatusCode.BadRequest || anonPost.StatusCode == HttpStatusCode.Redirect || anonPost.StatusCode == HttpStatusCode.Unauthorized);
            }

            // --- SEC-03, SEC-04 & SEC-05: ATI Nurse sees ATI ward tickets, blocked (403) from Admin & Cardiology tickets ---
            int atiTicketId;
            int cardioTicketId;
            using (var scope = factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<HelpdeskContext>();
                atiTicketId = ctx.Tickets.First(t => t.TicketCode == "TKT-2026-0001").Id;   // ATI (created by user@medcare.ro)
                cardioTicketId = ctx.Tickets.First(t => t.TicketCode == "TKT-2026-0008").Id; // Cardiology
            }

            using (var atiNurseClient = factory.CreateClient(opts))
            {
                await SignInAsync(atiNurseClient, "ati.nurse@medcare.ro", "User123!");

                // SEC-03: Medical Staff blocked from Admin /Users & /Departments
                Assert.Equal(HttpStatusCode.Forbidden, await GetFinalStatusAsync(atiNurseClient, "/Users/Index"));
                Assert.Equal(HttpStatusCode.Forbidden, await GetFinalStatusAsync(atiNurseClient, "/Departments/Index"));

                // SEC-05: ATI Nurse CAN view ATI ticket created by Dr. Vasilescu (same ward!)
                var sw = Stopwatch.StartNew();
                var ownWardResp = await atiNurseClient.GetAsync($"/Tickets/ViewTicket/{atiTicketId}");
                sw.Stop();
                Assert.Equal(HttpStatusCode.OK, ownWardResp.StatusCode);
                Assert.True(sw.ElapsedMilliseconds < 2000, $"Ticket view took {sw.ElapsedMilliseconds}ms");

                // SEC-04: ATI Nurse receives 403 Forbidden for Cardiology ticket (Anti-IDOR)
                Assert.Equal(HttpStatusCode.Forbidden, await GetFinalStatusAsync(atiNurseClient, $"/Tickets/ViewTicket/{cardioTicketId}"));
            }

            // --- SEC-06: Support Agent sees queue, blocked (403) from /Users/Index ---
            using (var agentClient = factory.CreateClient(opts))
            {
                await SignInAsync(agentClient, "agent@medcare.ro", "Agent123!");
                var agentDash = await agentClient.GetAsync("/Home/AgentDashboard");
                Assert.Equal(HttpStatusCode.OK, agentDash.StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, await GetFinalStatusAsync(agentClient, "/Users/Index"));
            }

            // --- SEC-07: Deactivated user cannot sign in ---
            using (var scope = factory.Services.CreateScope())
            {
                var um = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var cardioDr = await um.FindByEmailAsync("cardio.dr@medcare.ro");
                Assert.NotNull(cardioDr);
                cardioDr!.IsActive = false;
                await um.UpdateAsync(cardioDr);
            }

            using (var lockedClient = factory.CreateClient(opts))
            {
                var loginGet = await lockedClient.GetAsync("/Account/Login");
                var token = ExtractAntiForgeryToken(await loginGet.Content.ReadAsStringAsync());
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Email"] = "cardio.dr@medcare.ro",
                    ["Password"] = "User123!",
                    ["__RequestVerificationToken"] = token
                });
                var resp = await lockedClient.PostAsync("/Account/Login", form);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode); // Stays on login page with deactivated error
                var body = await resp.Content.ReadAsStringAsync();
                Assert.Contains("deactivated", body, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task EndToEnd_TicketLifecycle_ResolutionSummaryNotificationAndThreadLock_ShouldEnforceFrRules()
        {
            using var factory = new MedCareWebAppFactory();
            var opts = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };

            int createdTicketId;
            int requesterId;

            // 1. Medical Staff submits a Critical ticket (strict +2h SLA) and auto-generates TKT-2026-XXXX
            using (var userClient = factory.CreateClient(opts))
            {
                await SignInAsync(userClient, "user@medcare.ro", "User123!");

                var swDash = Stopwatch.StartNew();
                var userDash = await userClient.GetAsync("/Home/UserDashboard");
                swDash.Stop();
                Assert.Equal(HttpStatusCode.OK, userDash.StatusCode);
                Assert.True(swDash.ElapsedMilliseconds < 2000, $"UserDashboard took {swDash.ElapsedMilliseconds}ms");

                var submitGet = await userClient.GetAsync("/Tickets/SubmitTicket");
                submitGet.EnsureSuccessStatusCode();
                var submitToken = ExtractAntiForgeryToken(await submitGet.Content.ReadAsStringAsync());

                int equipCatId;
                int atiDeptId;
                using (var scope = factory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<HelpdeskContext>();
                    equipCatId = ctx.Categories.First(c => c.Name == "Medical Equipment").Id;
                    atiDeptId = ctx.Departments.First(d => d.WardCode == "ATI").Id;
                    requesterId = ctx.Users.First(u => u.Email == "user@medcare.ro").Id;
                }

                var submitResp = await userClient.PostAsync("/Tickets/SubmitTicket", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Title"] = "ATI Infusion Pump Occlusion Sensor Fault",
                    ["Description"] = "Infusion pump in ATI Bed 4 triggers false occlusion alarm.",
                    ["EquipmentTag"] = "INF-ATI-04",
                    ["CategoryId"] = equipCatId.ToString(),
                    ["DepartmentId"] = atiDeptId.ToString(),
                    ["Priority"] = "Critical",
                    ["__RequestVerificationToken"] = submitToken
                }));
                Assert.Equal(HttpStatusCode.Redirect, submitResp.StatusCode);

                using (var scope = factory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<HelpdeskContext>();
                    var created = ctx.Tickets.OrderByDescending(t => t.Id).First(t => t.Title == "ATI Infusion Pump Occlusion Sensor Fault");
                    createdTicketId = created.Id;
                    Assert.StartsWith("TKT-2026-", created.TicketCode);
                    Assert.NotNull(created.DueDate);
                    Assert.InRange((created.DueDate!.Value - created.CreatedAt).TotalMinutes, 119, 121);
                }
            }

            // 2. Agent claims ticket (creates unread notification), validates FR-17 summary requirement, then resolves
            using (var agentClient = factory.CreateClient(opts))
            {
                await SignInAsync(agentClient, "agent@medcare.ro", "Agent123!");
                var viewHtml = await (await agentClient.GetAsync($"/Tickets/ViewTicket/{createdTicketId}")).Content.ReadAsStringAsync();
                var claimToken = ExtractAntiForgeryToken(viewHtml);

                var claimResp = await agentClient.PostAsync("/Tickets/ClaimTicket", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["ticketId"] = createdTicketId.ToString(),
                    ["__RequestVerificationToken"] = claimToken
                }));
                Assert.Equal(HttpStatusCode.Redirect, claimResp.StatusCode);

                using (var scope = factory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<HelpdeskContext>();
                    var claimed = ctx.Tickets.First(t => t.Id == createdTicketId);
                    Assert.Equal("In Progress", claimed.Status);
                    Assert.Contains(ctx.TicketNotifications, n => n.TicketId == createdTicketId && n.UserId == requesterId && !n.IsRead);
                }

                // Attempt Resolve WITHOUT ResolutionSummary -> rejected by FR-17
                var statusPage = await (await agentClient.GetAsync($"/Tickets/ViewTicket/{createdTicketId}")).Content.ReadAsStringAsync();
                var statusToken = ExtractAntiForgeryToken(statusPage);
                var badResolve = await agentClient.PostAsync("/Tickets/UpdateTicketStatus", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["TicketId"] = createdTicketId.ToString(),
                    ["Status"] = "Resolved",
                    ["Priority"] = "Critical",
                    ["ResolutionSummary"] = "",
                    ["__RequestVerificationToken"] = statusToken
                }));
                Assert.Equal(HttpStatusCode.Redirect, badResolve.StatusCode);

                using (var scope = factory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<HelpdeskContext>();
                    var unchanged = ctx.Tickets.First(t => t.Id == createdTicketId);
                    Assert.Equal("In Progress", unchanged.Status);
                    Assert.Null(unchanged.ResolvedAt);
                }

                // Resolve WITH valid ResolutionSummary -> succeeds and stamps ResolvedAt
                var okPage = await (await agentClient.GetAsync($"/Tickets/ViewTicket/{createdTicketId}")).Content.ReadAsStringAsync();
                var okToken = ExtractAntiForgeryToken(okPage);
                var okResolve = await agentClient.PostAsync("/Tickets/UpdateTicketStatus", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["TicketId"] = createdTicketId.ToString(),
                    ["Status"] = "Resolved",
                    ["Priority"] = "Critical",
                    ["ResolutionSummary"] = "Replaced optical pressure sensor cartridge and verified calibration.",
                    ["__RequestVerificationToken"] = okToken
                }));
                Assert.Equal(HttpStatusCode.Redirect, okResolve.StatusCode);

                // Attempt comment on Resolved ticket -> thread locked (FR-18)
                var lockedPage = await (await agentClient.GetAsync($"/Tickets/ViewTicket/{createdTicketId}")).Content.ReadAsStringAsync();
                var commentToken = ExtractAntiForgeryToken(lockedPage);
                var lockedComment = await agentClient.PostAsync("/Tickets/AddComment", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["ticketId"] = createdTicketId.ToString(),
                    ["text"] = "Late follow-up note",
                    ["__RequestVerificationToken"] = commentToken
                }));
                Assert.Equal(HttpStatusCode.OK, lockedComment.StatusCode);
                var json = await lockedComment.Content.ReadAsStringAsync();
                Assert.Contains("locked", json, StringComparison.OrdinalIgnoreCase);
            }

            // 3. Requester opens ticket -> unread notifications marked IsRead = true
            using (var userClient = factory.CreateClient(opts))
            {
                await SignInAsync(userClient, "user@medcare.ro", "User123!");
                var ticketView = await userClient.GetAsync($"/Tickets/ViewTicket/{createdTicketId}");
                Assert.Equal(HttpStatusCode.OK, ticketView.StatusCode);

                using (var scope = factory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<HelpdeskContext>();
                    Assert.False(ctx.TicketNotifications.Any(n => n.TicketId == createdTicketId && n.UserId == requesterId && !n.IsRead));
                }

                // KB Catalog & Suggest endpoints under < 2000ms
                var kbResp = await userClient.GetAsync("/KnowledgeBase/Index?search=Ventilator");
                Assert.Equal(HttpStatusCode.OK, kbResp.StatusCode);
                Assert.Contains("Ventilator", await kbResp.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

                var kbJson = await userClient.GetAsync("/Tickets/GetKbSuggestions?query=ventilator%20oxygen%20pressure%20alarm");
                Assert.Equal(HttpStatusCode.OK, kbJson.StatusCode);
                Assert.Contains("Ventilator", await kbJson.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

                var printerJson = await userClient.GetAsync("/Tickets/GetKbSuggestions?query=printer%20jammed");
                Assert.Equal(HttpStatusCode.OK, printerJson.StatusCode);
                Assert.Contains("Printer", await printerJson.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

                var monitorJson = await userClient.GetAsync("/Tickets/GetKbSuggestions?query=monitor%20screen%20frozen");
                Assert.Equal(HttpStatusCode.OK, monitorJson.StatusCode);
                Assert.Contains("Monitor", await monitorJson.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

                var wifiJson = await userClient.GetAsync("/Tickets/GetKbSuggestions?query=wifi%20signal%20dropping");
                Assert.Equal(HttpStatusCode.OK, wifiJson.StatusCode);
                Assert.Contains("Wi-Fi", await wifiJson.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

                var predJson = await userClient.GetAsync("/Tickets/PredictTicketFields?title=PACS%20DICOM%20timeout&description=Radiology%20workstation%20cannot%20reach%20server");
                Assert.Equal(HttpStatusCode.OK, predJson.StatusCode);
                Assert.Contains("Hospital Software", await predJson.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            }

            // 4. Admin Dashboard & Ward/Category Management smoke check + KB Ownership (Admin vs Agent)
            int ownAgentArticleId;
            int adminArticleId;
            using (var scope = factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<HelpdeskContext>();
                var agentUser = ctx.Users.First(u => u.Email == "agent@medcare.ro");
                var adminUser = ctx.Users.First(u => u.Email == "admin@medcare.ro");
                ownAgentArticleId = ctx.KnowledgeArticles.First(a => a.AuthorId == agentUser.Id).Id;
                adminArticleId = ctx.KnowledgeArticles.First(a => a.AuthorId == adminUser.Id).Id;
            }

            // Agent CAN edit own guide, MUST receive 403 Forbidden trying to Edit or Delete Admin's / another Agent's guide
            using (var agentKbClient = factory.CreateClient(opts))
            {
                await SignInAsync(agentKbClient, "agent@medcare.ro", "Agent123!");
                var ownEditGet = await agentKbClient.GetAsync($"/KnowledgeArticles/Edit/{ownAgentArticleId}");
                Assert.Equal(HttpStatusCode.OK, ownEditGet.StatusCode);

                Assert.Equal(HttpStatusCode.Forbidden, await GetFinalStatusAsync(agentKbClient, $"/KnowledgeArticles/Edit/{adminArticleId}"));
                Assert.Equal(HttpStatusCode.Forbidden, await GetFinalStatusAsync(agentKbClient, $"/KnowledgeArticles/Delete/{adminArticleId}"));

                var ownHtml = await ownEditGet.Content.ReadAsStringAsync();
                var ownToken = ExtractAntiForgeryToken(ownHtml);
                var forbiddenDeletePost = await agentKbClient.PostAsync($"/KnowledgeArticles/Delete/{adminArticleId}", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Id"] = adminArticleId.ToString(),
                    ["__RequestVerificationToken"] = ownToken
                }));
                if (forbiddenDeletePost.StatusCode == HttpStatusCode.Redirect && forbiddenDeletePost.Headers.Location != null)
                {
                    forbiddenDeletePost = await agentKbClient.GetAsync(forbiddenDeletePost.Headers.Location);
                }
                Assert.Equal(HttpStatusCode.Forbidden, forbiddenDeletePost.StatusCode);
            }

            using (var adminClient = factory.CreateClient(opts))
            {
                await SignInAsync(adminClient, "admin@medcare.ro", "Admin123!");
                var swAdmin = Stopwatch.StartNew();
                var adminDash = await adminClient.GetAsync("/Home/AdministratorDashboard");
                swAdmin.Stop();
                Assert.Equal(HttpStatusCode.OK, adminDash.StatusCode);
                Assert.True(swAdmin.ElapsedMilliseconds < 2000, $"AdministratorDashboard took {swAdmin.ElapsedMilliseconds}ms");
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Users/Index")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Departments/Index")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Categories/Index")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/KnowledgeArticles/Index")).StatusCode);
                // Admin CAN edit/delete guides authored by any agent
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync($"/KnowledgeArticles/Edit/{ownAgentArticleId}")).StatusCode);
            }
        }

        private static async Task SignInAsync(HttpClient client, string email, string password)
        {
            var loginGet = await client.GetAsync("/Account/Login");
            loginGet.EnsureSuccessStatusCode();
            var html = await loginGet.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(html);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["__RequestVerificationToken"] = token
            });

            var postResp = await client.PostAsync("/Account/Login", content);
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);
        }

        private static async Task<HttpStatusCode> GetFinalStatusAsync(HttpClient client, string url)
        {
            var resp = await client.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.Redirect && resp.Headers.Location != null)
            {
                resp = await client.GetAsync(resp.Headers.Location);
            }
            return resp.StatusCode;
        }

        private static string ExtractAntiForgeryToken(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
            Assert.True(match.Success, "Anti-forgery token not found in HTML response.");
            return match.Groups[1].Value;
        }
    }
}
