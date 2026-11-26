# MedCare HelpDesk (`SPM-HC-2026-01`)

> **Hospital Support Ticketing, SLA Monitoring & Self-Service Knowledge Base Platform**  
> **Project Sponsor:** REGINA MARIA Private Healthcare Network – Craiova (Dr. Claudiu Prica, Medical Director)  
> **Course:** Software Project Management (Coord. Prof. Liliana Popa)  
> **Schedule:** October 19, 2026 – January 22, 2027 (6 Phases / 48 WBS Work Packages / 4 Sprints)

---

## 1. Project Overview

**MedCare HelpDesk** is a centralized ASP.NET Core MVC web application built for hospital wards (ATI / ICU, Emergency / UPU, Surgery, Cardiology, Radiology, Laboratory, Pediatrics, and Administration) to replace ad-hoc phone calls and paper logs with structured incident reporting, automated 24/7 SLA urgency tracking (**2h / 4h / 12h / 24h**), secure photo/PDF attachment diagnostics, role-based access control, intelligent ticket triage (ML.NET classification, TF-IDF Knowledge Base suggestions, and weighted least-loaded technician routing), and a searchable Knowledge Base.

### Strict Healthcare Privacy Boundary
- **Zero Patient PII/PHI:** The platform strictly tracks **technical equipment, clinical software, network infrastructure, and staff support requests** — never patient names, CNPs, diagnoses, or medical records.
- **Connectivity Fallback:** Persistent **Emergency Technical Hotline (`+40 251-XXX-911` / `Ext. 911`)** displayed across the UI in case of local hospital Wi-Fi drops.

---

## 2. Team Members & Responsibilities

| Member | Role | GitHub Handle | Core Ownership |
| :--- | :--- | :--- | :--- |
| **Ana-Maria-Cristina Peca** | Project Manager | `@peca-ana-ace-ucv` | Project Charter, Scope Statement, WBS, Risk Management, Weekly Status, Final Wrap-Up |
| **Alexandru-Cristian-George Șerban** | Lead Back-End Developer | `@AlexandruCristianGeorgeSerban` | N-Tier Architecture, Git Setup, SLA Engine (`2/4/12/24h`), Ticket Service, Smart Routing & Optimization |
| **Darius-Adonis-Ionuț Ploae** | Database Engineer | `@PLOAE-DARIUS-ADONIS-IONUT` | ERD Schema, EF Core SQLite Migrations, Repositories/UoW, Hospital Wards CRUD, `DbSeeder` |
| **David-Andrei Nică** | Security & Identity Developer | `@NicaDavidAndrei` | ASP.NET Core Identity (`Admin`, `Agent`, `User`), Guest Portal, Ward Anti-IDOR, Author/Admin KB Guards |
| **Andrei Popa-Ion** | Lead Front-End Developer | `@Popa-Ion-Andrei` | Clinical Navy & Teal Sidebar UI, Split-Screen Login, SLA Progress Rails, Dark ICU Night-Shift Mode |
| **Iulian-Marian Oprea** | Full-Stack Developer | `@OIulian16` | Two-Column `<60s` Ticket Form with Live ML.NET/TF-IDF Panel, Magic-Byte Photo/PDF Uploads, Comments |
| **Oana-Adriana Stan** | Business Analyst & Tech Writer | `@Stan-Oana` | Hospital Staff Interviews (ATI/UPU), FR-01..FR-28, Use-Case/Activity Diagrams, KB Service, User Manual |
| **Ștefan-Andrei Necșulea** | QA Engineer | `@NecsuleaStefan` | QA Rules & Checklist, Master Test Plan, Test Scenarios, xUnit & Security Testing (`SEC-01..SEC-08`), QA Report |

---

## 3. Technology Stack & Architecture

- **Framework:** ASP.NET Core MVC (`.NET 10.0`, C# Nullable & ImplicitUsings)
- **Database & ORM:** **SQLite** (`MedCareHelpdesk.db`) via `Microsoft.EntityFrameworkCore.Sqlite` & Code-First Migrations
- **Security:** ASP.NET Core Identity (`User : IdentityUser<int>`, `IdentityRole<int>`), PBKDF2 password hashing, lockout (`5` attempts / `10` min), `[Authorize(Roles = "...")]`, `[ValidateAntiForgeryToken]`, Ward-scoped Anti-IDOR checks, extension + magic-byte upload validation (`.jpg`, `.jpeg`, `.png`, `.pdf` `<= 5 MB`)
- **Intelligent Triage & NLP:**
  - **ML.NET (`SdcaMaximumEntropy`):** Automatically predicts problem Category, Hospital Ward/Department, and Priority while typing (`SubmitTicket`), dynamically hiding `AI` badges when overridden manually.
  - **TF-IDF + Cosine Similarity:** HTML-stripped, stemmed TF-IDF Knowledge Base suggestions while typing a ticket, plus Similar Resolved Tickets in `ViewTicket`.
  - **Weighted Least-Loaded Routing:** Assigns open tickets to the least-loaded active `Agent` (`Critical=4`, `High=3`, `Medium=2`, `Low=1`) matched by specialty/department.
- **Front-End:** Razor Views, Bootstrap 5, Bootstrap Icons, jQuery + Unobtrusive Validation, Dark/Light Theme toggle (`localStorage`)
- **Testing & CI:** `xUnit` test suite (`MedCareHelpDesk.Tests`) + GitHub Actions CI (`.github/workflows/ci.yml`)

---

## 4. SLA Response & Resolution Policy (24/7 Hospital Clock)

| Priority Tier | First Response Target | Resolution SLA (`DueDate`) | Amber Warning (`<= 25%` left) | Clinical Trigger |
| :--- | :--- | :--- | :--- | :--- |
| **Critical** | 15 minutes | **2 Hours** | `<= 30 min` remaining | ATI (ICU) / Emergency (UPU) life-support, monitors, triage HIS outage |
| **High** | 30 minutes | **4 Hours** | `<= 1 hour` remaining | Surgery / Radiology / Lab clinical disruption with temporary backup |
| **Medium** | 2 hours | **12 Hours** | `<= 3 hours` remaining | Ward printers, non-critical PC glitches, localized Wi-Fi slowness |
| **Low** | 4 hours | **24 Hours** | `<= 6 hours` remaining | Routine requests, profile ward updates, new KB article requests |

---

## 5. System Roles & Demo Accounts (Seeded on Startup)

| Role Key | Display Name | Landing Dashboard | Permissions Scope | Demo Account | Password |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Admin` | Administrator | `/Home/AdministratorDashboard` | Full governance (Users, Wards, Categories, Tickets, All KB Guides, KPIs) | `admin@medcare.ro` | `Admin123!` |
| `Agent` | Support Agent | `/Home/AgentDashboard` | Global SLA queue, Claim/Update/Resolve tickets, Author & manage **own** KB guides | `agent@medcare.ro` | `Agent123!` |
| `User` | Medical Staff | `/Home/UserDashboard` | Submit `<60s` tickets (ML.NET + TF-IDF), View/Comment own & home-ward tickets, Search KB | `user@medcare.ro` | `User123!` |
| `—` | **Guest** | `/Home/GuestDashboard` | Read-only portal: Search KB, view 24/7 SLA policy & Emergency Hotline | *(Public access)* | `—` |

*(Additional ward clinicians and specialized Biomedical/IT support agents are pre-seeded in `Data/DbSeeder.cs`.)*

---

## 6. Repository Structure

```text
SPM_Project_MedCare_HelpDesk/
├── .github/workflows/ci.yml                       # Automated build & xUnit CI workflow
├── Architecture_Planning Technology_Stack/        # WBS 1.5 Architecture & Stack Spec
├── Functional_Requirements/                       # WBS 2.2 FR-01..FR-28 & SLA Spec
├── Interview/                                     # WBS 2.1 ATI & Emergency Interviews
├── Non_Functional Security_Specifications/        # WBS 2.3 NFR, SLA & Security Spec
├── Project_Charter Scope_Statement WBS/           # WBS 1.1-1.4 Charter, Scope, WBS (.drawio/.png), Dictionary, Activity List
├── Management_QA_Meetings/                        # WBS 1.6 QA Rules, WBS 1.7 Meeting Minutes, WBS 1.8 Wrap-Up
├── Requirements_Analysis/                         # WBS 2.5 Wards/Equipment, 2.6 Use-Cases/UI, 2.7 Form/KB, 2.8 Test Plan
├── System_Design_Diagrams/                        # WBS 3.1-3.8 UML Diagrams, ERD, Wireframes, Test Scenarios
├── Handover_QA_Reports/                           # WBS 6.6 QA Report, 6.7 Deployment Guide, 6.8 User Manual & Final Slides
├── Resources/                                     # Resource Planning, RACI, Gantt, Monday.com exports
├── Risk_Matrix RBS/                               # WBS 1.4 Risk Matrix, RBS & Register (.pptx)
├── Role_Access/                                   # WBS 2.4 RBAC Matrix & SEC-01..SEC-08
├── SCRUM/                                         # Backlog, Sprint Plan & Story Cards
└── MedCare HelpDesk/                              # ASP.NET Core MVC Solution (.NET 10 + SQLite)
    ├── MedCare HelpDesk.slnx
    ├── MedCare HelpDesk/                          # Controllers, Models, Repositories, Services, Views, Migrations, wwwroot
    └── MedCareHelpDesk.Tests/                     # xUnit Automated Test Suite
```

---

## 7. Quick Start (Build, Run & Test)

```bash
cd "MedCare HelpDesk"
dotnet restore
dotnet build
dotnet test
dotnet run --project "MedCare HelpDesk/MedCare HelpDesk.csproj"
```

On first launch, `Program.cs` automatically applies EF Core SQLite migrations (`20261126183342_InitialMedCareSqlite` -> `MedCareHelpdesk.db`), seeds the 3 authenticated roles (`Admin`, `Agent`, `User`), 8 hospital wards, 4 problem categories, 9 staff/agent/admin accounts, realistic clinical tickets, comments, notifications, and 10 author-linked Knowledge Base guides, and trains the ML.NET triage model.
