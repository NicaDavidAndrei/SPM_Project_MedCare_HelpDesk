using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedCare_HelpDesk.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider serviceProvider,
            HelpdeskContext context,
            UserManager<User> userManager,
            RoleManager<IdentityRole<int>> roleManager)
        {
            // 1. SEED 8 HOSPITAL WARDS (Departments)
            if (!context.Departments.Any())
            {
                context.Departments.AddRange(
                    new Department { Name = "ATI (Intensive Care Unit)", WardCode = "ATI", WardPhoneExtension = "Ext. 101", IsCriticalCare = true, IsActive = true },
                    new Department { Name = "Emergency (UPU)", WardCode = "UPU", WardPhoneExtension = "Ext. 102", IsCriticalCare = true, IsActive = true },
                    new Department { Name = "Surgery (Operating Rooms)", WardCode = "SURG", WardPhoneExtension = "Ext. 103", IsCriticalCare = false, IsActive = true },
                    new Department { Name = "Cardiology", WardCode = "CARD", WardPhoneExtension = "Ext. 104", IsCriticalCare = false, IsActive = true },
                    new Department { Name = "Radiology & Imaging", WardCode = "RAD", WardPhoneExtension = "Ext. 105", IsCriticalCare = false, IsActive = true },
                    new Department { Name = "Clinical Laboratory", WardCode = "LAB", WardPhoneExtension = "Ext. 106", IsCriticalCare = false, IsActive = true },
                    new Department { Name = "Pediatrics", WardCode = "PED", WardPhoneExtension = "Ext. 107", IsCriticalCare = false, IsActive = true },
                    new Department { Name = "Administration & Support", WardCode = "ADM", WardPhoneExtension = "Ext. 108", IsCriticalCare = false, IsActive = true }
                );
                await context.SaveChangesAsync();
            }

            var deptMap = await context.Departments.ToDictionaryAsync(d => d.Name!, d => d.Id);

            // 2. SEED 4 PROBLEM CATEGORIES
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "Medical Equipment", Description = "Ventilators, multiparameter bedside monitors, infusion pumps, defibrillators, analyzers.", SpecialtyTag = "Biomedical Engineering", IconClass = "bi-heart-pulse", IsActive = true },
                    new Category { Name = "Hospital Software", Description = "HIS clinical modules, Radiology PACS viewer, Lab LIS interfaces.", SpecialtyTag = "Clinical IT Support", IconClass = "bi-window-stack", IsActive = true },
                    new Category { Name = "IT / Network", Description = "Hospital Wi-Fi, Ethernet links, barcode/thermal printers, ward PCs.", SpecialtyTag = "Network & Infrastructure", IconClass = "bi-wifi", IsActive = true },
                    new Category { Name = "Staff Requests", Description = "Account provisioning, ward transfer phone updates, Knowledge Base requests.", SpecialtyTag = "Facility & Access Admin", IconClass = "bi-person-gear", IsActive = true }
                );
                await context.SaveChangesAsync();
            }

            var catMap = await context.Categories.ToDictionaryAsync(c => c.Name!, c => c.Id);

            // 3. SEED 3 AUTHENTICATED SYSTEM ROLES (Guest = Unauthenticated Visitor)
            string[] roles = { "Admin", "Agent", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<int> { Name = role });
                }
            }

            // 4. SEED HOSPITAL ACCOUNTS ACROSS ALL 4 ROLES
            var usersToSeed = new[]
            {
                new
                {
                    User = new User
                    {
                        UserName = "admin@medcare.ro",
                        Email = "admin@medcare.ro",
                        FirstName = "Cristian",
                        LastName = "Marin",
                        Role = "Admin",
                        DepartmentId = deptMap["Administration & Support"],
                        PhoneNumber = "+40 251-400-108",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = true,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "Admin123!",
                    Role = "Admin"
                },
                new
                {
                    User = new User
                    {
                        UserName = "agent@medcare.ro",
                        Email = "agent@medcare.ro",
                        FirstName = "Radu",
                        LastName = "Ionescu",
                        Role = "Agent",
                        DepartmentId = deptMap["ATI (Intensive Care Unit)"],
                        PhoneNumber = "+40 251-400-201",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = true,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "Agent123!",
                    Role = "Agent"
                },
                new
                {
                    User = new User
                    {
                        UserName = "biomed.agent@medcare.ro",
                        Email = "biomed.agent@medcare.ro",
                        FirstName = "Andrei",
                        LastName = "Vasile",
                        Role = "Agent",
                        DepartmentId = deptMap["Surgery (Operating Rooms)"],
                        PhoneNumber = "+40 251-400-202",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = true,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "Agent123!",
                    Role = "Agent"
                },
                new
                {
                    User = new User
                    {
                        UserName = "it.agent@medcare.ro",
                        Email = "it.agent@medcare.ro",
                        FirstName = "Mihai",
                        LastName = "Popescu",
                        Role = "Agent",
                        DepartmentId = deptMap["Radiology & Imaging"],
                        PhoneNumber = "+40 251-400-203",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = true,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "Agent123!",
                    Role = "Agent"
                },
                new
                {
                    User = new User
                    {
                        UserName = "net.agent@medcare.ro",
                        Email = "net.agent@medcare.ro",
                        FirstName = "Ioana",
                        LastName = "Marinescu",
                        Role = "Agent",
                        DepartmentId = deptMap["Emergency (UPU)"],
                        PhoneNumber = "+40 251-400-204",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = false,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "Agent123!",
                    Role = "Agent"
                },
                new
                {
                    User = new User
                    {
                        UserName = "user@medcare.ro",
                        Email = "user@medcare.ro",
                        FirstName = "Elena",
                        LastName = "Vasilescu",
                        Role = "User",
                        DepartmentId = deptMap["ATI (Intensive Care Unit)"],
                        PhoneNumber = "+40 251-400-101",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = true,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "User123!",
                    Role = "User"
                },
                new
                {
                    User = new User
                    {
                        UserName = "ati.nurse@medcare.ro",
                        Email = "ati.nurse@medcare.ro",
                        FirstName = "Gabriela",
                        LastName = "Munteanu",
                        Role = "User",
                        DepartmentId = deptMap["ATI (Intensive Care Unit)"],
                        PhoneNumber = "+40 251-400-101",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = true,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "User123!",
                    Role = "User"
                },
                new
                {
                    User = new User
                    {
                        UserName = "upu.nurse@medcare.ro",
                        Email = "upu.nurse@medcare.ro",
                        FirstName = "Sorin",
                        LastName = "Dumitrescu",
                        Role = "User",
                        DepartmentId = deptMap["Emergency (UPU)"],
                        PhoneNumber = "+40 251-400-102",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = true,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "User123!",
                    Role = "User"
                },
                new
                {
                    User = new User
                    {
                        UserName = "cardio.dr@medcare.ro",
                        Email = "cardio.dr@medcare.ro",
                        FirstName = "Alexandru",
                        LastName = "Stănescu",
                        Role = "User",
                        DepartmentId = deptMap["Cardiology"],
                        PhoneNumber = "+40 251-400-104",
                        EmailNotificationsEnabled = true,
                        PushNotificationsEnabled = false,
                        IsActive = true,
                        EmailConfirmed = true
                    },
                    Password = "User123!",
                    Role = "User"
                }
            };

            foreach (var entry in usersToSeed)
            {
                var existing = await userManager.FindByEmailAsync(entry.User.Email!);
                if (existing == null)
                {
                    var createResult = await userManager.CreateAsync(entry.User, entry.Password);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(entry.User, entry.Role);
                    }
                }
            }

            var userMap = await context.Users.ToDictionaryAsync(u => u.Email!, u => u.Id);

            // 5. SEED HOSPITAL KNOWLEDGE BASE ARTICLES
            if (!context.KnowledgeArticles.Any())
            {
                context.KnowledgeArticles.AddRange(
                    new KnowledgeArticle
                    {
                        Title = "ATI Bedside Monitor SpO2 & ECG Signal Recalibration",
                        Category = "Medical Equipment",
                        IconClass = "bi-heart-pulse",
                        ColorClass = "text-danger",
                        AuthorId = userMap["agent@medcare.ro"],
                        AuthorName = "Radu Ionescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-5),
                        Content = "<ol><li>Verify the SpO2 finger sensor cable is firmly seated into Port B on the bedside monitor.</li><li>Hold the <strong>Zero / Calibrate</strong> soft key for 3 seconds until the green indicator stabilizes.</li><li>If the alarm persists beyond 60 seconds, switch to the bay backup unit and submit a <strong>Critical (2h)</strong> ticket immediately.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "Volumetric Infusion Pump Battery & AC Power Reset",
                        Category = "Medical Equipment",
                        IconClass = "bi-battery-charging",
                        ColorClass = "text-primary",
                        AuthorId = userMap["biomed.agent@medcare.ro"],
                        AuthorName = "Andrei Vasile",
                        UpdatedAt = DateTime.UtcNow.AddDays(-4),
                        Content = "<ol><li>Confirm the hospital-grade AC cord is plugged into an uninterrupted red ATI outlet.</li><li>Check the rear fuse indicator LED; a blinking amber LED indicates battery pack replacement needed.</li><li>Swap to a charged reserve pump before disconnecting power and log a High priority ticket with the asset tag.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "UPU Defibrillator Daily Self-Test Error Codes",
                        Category = "Medical Equipment",
                        IconClass = "bi-activity",
                        ColorClass = "text-danger",
                        AuthorId = userMap["agent@medcare.ro"],
                        AuthorName = "Radu Ionescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-3),
                        Content = "<ol><li>Inspect pad connector leads for moisture or bent pins.</li><li>Run a 30J internal test load with test plug attached.</li><li>Attach a smartphone photo of the self-test error code screen to a Critical ticket so Biomedical brings the exact module.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "HIS Clinical Chart Session Timeout & Lock Recovery",
                        Category = "Hospital Software",
                        IconClass = "bi-window-stack",
                        ColorClass = "text-warning",
                        AuthorId = userMap["it.agent@medcare.ro"],
                        AuthorName = "Mihai Popescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-6),
                        Content = "<ol><li>Press <strong>Ctrl + Shift + R</strong> to refresh the ward HIS terminal session cache without losing unsaved drafts.</li><li>Ensure Caps Lock is off before re-entering institutional credentials.</li><li>If the triage module remains frozen for >2 minutes, open a Hospital Software ticket or call Ext. 911.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "Radiology PACS DICOM Viewer Cache Clear",
                        Category = "Hospital Software",
                        IconClass = "bi-file-earmark-medical",
                        ColorClass = "text-info",
                        AuthorId = userMap["it.agent@medcare.ro"],
                        AuthorName = "Mihai Popescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-3),
                        Content = "<ol><li>Close the PACS diagnostic workstation viewer cleanly from the system tray.</li><li>Run the desktop shortcut <em>Clear PACS Local Study Cache</em>.</li><li>Relaunch the viewer and reload the study; if DICOM transfer stalls, submit a High priority ticket.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "Lab Analyzer LIS Barcode Synchronization Reset",
                        Category = "Hospital Software",
                        IconClass = "bi-eyedropper",
                        ColorClass = "text-primary",
                        AuthorId = userMap["it.agent@medcare.ro"],
                        AuthorName = "Mihai Popescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-2),
                        Content = "<ol><li>Verify the HL7 bridge icon in the Lab workstation taskbar is green.</li><li>Click <strong>Reconnect LIS Queue</strong> and wait 15 seconds for queued sample results.</li><li>Never restart the analyzer mid-run; escalate via Hospital Software ticket if pending >10 minutes.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "Emergency Wristband Thermal Printer Jam & Media Calibration",
                        Category = "IT / Network",
                        IconClass = "bi-printer",
                        ColorClass = "text-success",
                        AuthorId = userMap["net.agent@medcare.ro"],
                        AuthorName = "Ioana Marinescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-4),
                        Content = "<ol><li>Open the top cover and check for peeled patient wristband adhesive around the thermal head.</li><li>Clean gently with an isopropyl alcohol swab and reseat the roll between the blue guides.</li><li>Hold the Feed button until the status LED flashes twice for automatic label calibration.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "Hospital Wi-Fi Dead Zone & Emergency Hotline Protocol",
                        Category = "IT / Network",
                        IconClass = "bi-wifi",
                        ColorClass = "text-success",
                        AuthorId = userMap["net.agent@medcare.ro"],
                        AuthorName = "Ioana Marinescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-2),
                        Content = "<ol><li>Toggle Wi-Fi off/on on the ward tablet to force re-roaming to the nearest hospital access point (<code>MEDCARE-CLINIC</code>).</li><li>For urgent ATI or Emergency (UPU) faults during local Wi-Fi drops, immediately dial the <strong>Emergency Technical Hotline: +40 251-XXX-911</strong>.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "ATI Mechanical Ventilator Oxygen Flow & Pressure Calibration",
                        Category = "Medical Equipment",
                        IconClass = "bi-lungs",
                        ColorClass = "text-danger",
                        AuthorId = userMap["agent@medcare.ro"],
                        AuthorName = "Radu Ionescu",
                        UpdatedAt = DateTime.UtcNow.AddDays(-2),
                        Content = "<ol><li>Inspect the inspiratory limb circuit and water trap for condensation or loose cuff seals.</li><li>Verify wall medical O2 and air manifold gauges read between 3.5 and 5.0 bar.</li><li>Run the pre-use leak and pressure sensor test when patient is safely on backup ventilation, or log a Critical (2h) ticket with the asset tag.</li></ol>"
                    },
                    new KnowledgeArticle
                    {
                        Title = "Updating Ward Phone Extension & Account Security Policy",
                        Category = "Staff Requests",
                        IconClass = "bi-shield-check",
                        ColorClass = "text-secondary",
                        AuthorId = userMap["admin@medcare.ro"],
                        AuthorName = "Cristian Marin",
                        UpdatedAt = DateTime.UtcNow.AddDays(-1),
                        Content = "<ol><li>Open <strong>My Profile</strong> from the top-right user menu and update your direct ward phone extension so technicians can reach your bay immediately.</li><li>Passwords require at least 8 characters with uppercase, lowercase, and numeric digits; 5 failed logins trigger a 10-minute lockout.</li></ol>"
                    }
                );
                await context.SaveChangesAsync();
            }

            // 6. SEED REALISTIC HOSPITAL TICKETS (Demonstrating 2/4/12/24h SLA states)
            if (!context.Tickets.Any())
            {
                var now = DateTime.UtcNow;

                var t1Created = now.AddHours(-3); // Critical 2h -> Breached!
                var t2Created = now.AddMinutes(-95); // Critical 2h -> ~25 min left (<=25% Approaching Amber!)
                var t3Created = now.AddMinutes(-20); // Critical 2h -> On Track
                var t4Created = now.AddHours(-2); // High 4h -> Pending Info
                var t5Created = now.AddHours(-5); // Medium 12h -> In Progress
                var t6Created = now.AddHours(-6); // High 4h -> Resolved ON TIME
                var t7Created = now.AddHours(-10); // Critical 2h -> Resolved LATE
                var t8Created = now.AddHours(-15); // Low 24h -> Closed ON TIME

                var tickets = new List<Ticket>
                {
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0001",
                        Title = "ATI Bed 2 Mechanical Ventilator Pressure Sensor Fault",
                        Description = "Ventilator screen displays intermittent pressure calibration alarm; backup unit connected on Bed 2. Immediate biomedical inspection needed.",
                        EquipmentTag = "VENT-ATI-02",
                        Status = "Open",
                        Priority = "Critical",
                        CreatedAt = t1Created,
                        DueDate = TicketService.CalculateDueDateStatic("Critical", t1Created),
                        CategoryId = catMap["Medical Equipment"],
                        DepartmentId = deptMap["ATI (Intensive Care Unit)"],
                        RequesterId = userMap["user@medcare.ro"],
                        AssignedAgentId = userMap["agent@medcare.ro"]
                    },
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0002",
                        Title = "UPU Resuscitation Bay Defibrillator Self-Test Code E-42",
                        Description = "Morning self-test returned error code E-42 on pad impedance check. Photo of error screen ready for technician.",
                        EquipmentTag = "DEFIB-UPU-01",
                        Status = "In Progress",
                        Priority = "Critical",
                        CreatedAt = t2Created,
                        DueDate = TicketService.CalculateDueDateStatic("Critical", t2Created),
                        CategoryId = catMap["Medical Equipment"],
                        DepartmentId = deptMap["Emergency (UPU)"],
                        RequesterId = userMap["upu.nurse@medcare.ro"],
                        AssignedAgentId = userMap["net.agent@medcare.ro"]
                    },
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0003",
                        Title = "ATI Bed 4 Multiparameter Monitor SpO2 Module Freeze",
                        Description = "Waveform display froze after cable replacement; reboot did not restore SpO2 telemetry stream.",
                        EquipmentTag = "MON-ATI-04",
                        Status = "Open",
                        Priority = "Critical",
                        CreatedAt = t3Created,
                        DueDate = TicketService.CalculateDueDateStatic("Critical", t3Created),
                        CategoryId = catMap["Medical Equipment"],
                        DepartmentId = deptMap["ATI (Intensive Care Unit)"],
                        RequesterId = userMap["ati.nurse@medcare.ro"],
                        AssignedAgentId = null // Unassigned so agents can test quick 'Assign to Me'
                    },
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0004",
                        Title = "Radiology PACS Viewer Crash Loading CT Multi-Slice Study",
                        Description = "Workstation 3 closes DICOM viewer during 3D reconstruction; secondary console available.",
                        EquipmentTag = "PACS-RAD-03",
                        Status = "Pending Info",
                        Priority = "High",
                        CreatedAt = t4Created,
                        DueDate = TicketService.CalculateDueDateStatic("High", t4Created),
                        CategoryId = catMap["Hospital Software"],
                        DepartmentId = deptMap["Radiology & Imaging"],
                        RequesterId = userMap["cardio.dr@medcare.ro"],
                        AssignedAgentId = userMap["it.agent@medcare.ro"]
                    },
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0005",
                        Title = "UPU Triage Barcode Wristband Printer Spooler Jam",
                        Description = "Thermal printer at Triage Desk 1 stalls after every 4th wristband print.",
                        EquipmentTag = "PRN-UPU-01",
                        Status = "In Progress",
                        Priority = "Medium",
                        CreatedAt = t5Created,
                        DueDate = TicketService.CalculateDueDateStatic("Medium", t5Created),
                        CategoryId = catMap["IT / Network"],
                        DepartmentId = deptMap["Emergency (UPU)"],
                        RequesterId = userMap["upu.nurse@medcare.ro"],
                        AssignedAgentId = userMap["net.agent@medcare.ro"]
                    },
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0006",
                        Title = "ATI Syringe Infusion Pump Power Module Alert",
                        Description = "Pump B3 alerts on battery discharge when moved during patient ward transfer.",
                        EquipmentTag = "PUMP-ATI-03",
                        Status = "Resolved",
                        Priority = "High",
                        CreatedAt = t6Created,
                        DueDate = TicketService.CalculateDueDateStatic("High", t6Created),
                        ResolvedAt = t6Created.AddHours(2),
                        ResolutionSummary = "Replaced degraded internal NiMH battery pack and verified 4-hour autonomy test.",
                        CategoryId = catMap["Medical Equipment"],
                        DepartmentId = deptMap["ATI (Intensive Care Unit)"],
                        RequesterId = userMap["user@medcare.ro"],
                        AssignedAgentId = userMap["agent@medcare.ro"]
                    },
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0007",
                        Title = "Surgery OR-2 Overhead Surgical Lamp Dimmer Flicker",
                        Description = "Main surgical dome flickers at 80% intensity during pre-op check.",
                        EquipmentTag = "LAMP-OR-02",
                        Status = "Resolved",
                        Priority = "Critical",
                        CreatedAt = t7Created,
                        DueDate = TicketService.CalculateDueDateStatic("Critical", t7Created),
                        ResolvedAt = t7Created.AddMinutes(165), // 2h 45m -> Resolved Late against 2h SLA
                        ResolutionSummary = "Replaced faulty PSU driver board in ceiling control cabinet; brightness calibrated.",
                        CategoryId = catMap["Medical Equipment"],
                        DepartmentId = deptMap["Surgery (Operating Rooms)"],
                        RequesterId = userMap["cardio.dr@medcare.ro"],
                        AssignedAgentId = userMap["biomed.agent@medcare.ro"]
                    },
                    new Ticket
                    {
                        TicketCode = "TKT-2026-0008",
                        Title = "Update Cardiology Nursing Station Keyboard & Phone Ext",
                        Description = "Requesting spare wireless keyboard and confirmation of Cardiology extension in directory.",
                        EquipmentTag = "PC-CARD-01",
                        Status = "Closed",
                        Priority = "Low",
                        CreatedAt = t8Created,
                        DueDate = TicketService.CalculateDueDateStatic("Low", t8Created),
                        ResolvedAt = t8Created.AddHours(5),
                        ResolutionSummary = "Installed new USB keyboard and verified Ext. 104 routing.",
                        CategoryId = catMap["Staff Requests"],
                        DepartmentId = deptMap["Cardiology"],
                        RequesterId = userMap["cardio.dr@medcare.ro"],
                        AssignedAgentId = userMap["it.agent@medcare.ro"]
                    }
                };

                context.Tickets.AddRange(tickets);
                await context.SaveChangesAsync();

                // 7. SEED SAMPLE COMMENTS & UNREAD NOTIFICATIONS
                context.Comments.AddRange(
                    new Comment
                    {
                        TicketId = tickets[1].Id,
                        UserId = userMap["net.agent@medcare.ro"],
                        Text = "On my way to UPU Resuscitation Bay with spare pad impedance test cable.",
                        CreatedAt = now.AddMinutes(-35)
                    },
                    new Comment
                    {
                        TicketId = tickets[5].Id,
                        UserId = userMap["agent@medcare.ro"],
                        Text = "Battery module swapped and load-tested on ATI bench. Pump returned to Bed 3.",
                        CreatedAt = t6Created.AddHours(2)
                    }
                );

                context.TicketNotifications.Add(
                    new TicketNotification
                    {
                        UserId = userMap["user@medcare.ro"],
                        TicketId = tickets[5].Id,
                        Message = "Ticket TKT-2026-0006 (ATI Syringe Infusion Pump Power Module Alert) was Resolved: battery pack replaced.",
                        IsRead = false,
                        CreatedAt = t6Created.AddHours(2)
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}
