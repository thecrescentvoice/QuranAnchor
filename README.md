# Quran Anchor Portal

ASP.NET Core MVC (.NET 10), EF Core with MySQL, Clean Architecture (Domain, Application, Infrastructure, Presentation).
This is the whole portal from the PRD, written in one pass. **It has not been compiled or run yet** (the environment it was written in had no .NET SDK). Expect to fix a few build errors on the first build.

## What is in it

**Accounts and access**
- Sign-in with Student ID (students and parents) or email (staff). Lockout after 5 wrong passwords, idle sessions end after 2 hours, rate limit on sign-in and reset.
- Forgot password by email (one-time link, 1 hour). Siblings sharing the parent's email each get their own link.
- Temporary passwords: new accounts must choose their own before using anything.
- Five roles with permissions: Super Admin, Admin (also moderator), Supervisor, Tutor, Student. You can only manage roles below your own.
- Two-step verification (authenticator app) required for Super Admin and Admin.
- Privacy notice and parent consent at first sign-in (version recorded; raise `PrivacyNotice.CurrentVersion` to ask again).
- Guided Create student form (auto IDs like QA-STU-0007, weekly slots, Meet link, level), tutors, staff.
- Deactivate and Reactivate (history kept). Permanent delete: Super Admin only. Replace tutor (moves students and upcoming classes, signs the old tutor out, shows a "replace the Meet link" checklist).
- Audit log of sensitive actions.

**Classes**
- Weekly schedule per student (local time plus time zone, so daylight saving never shifts a class). Sessions are created automatically for the current month and stop at the package total.
- Monthly packages by calendar month, sessions per month chosen per student, leftover sessions carry over to the next month. Mid-month joiners get a first package for the rest of the month.
- Double-booking check for tutors, break days (Eid etc.), student pause (on hold), extra single classes.
- Reschedule one class: request, then the other side accepts (supervisor can override).
- "Class not held" with reasons; a student marked absent only after a waiting time; parent is emailed.
- Join class button (opens 10 minutes before until the end; never locked by the fee). One permanent Meet link per student, visible to admin and supervisor, with Replace link.

**Learning records**
- Feedback form after every class (your format), with Goal Achieved Yes / Partially / No, and Copy as WhatsApp message.
- Monthly plan (weekly rows become the learning objective in the feedback form) with PDF download.
- Monthly report calculated from class records, tutor's closing remarks, publish, and PDF download.
- Missing-feedback list and supervisor email alerts.

**Money**
- Portal fee in NGN, USD and GBP (set on the Pricing page, default ₦2,000 / $1.50 / £1.15) through Paystack. The server sets the amount; only Paystack's confirmation (webhook or verify call) is trusted; each reference counts once. Manual "mark paid" and "waive fee".
- Overdue fee: after the grace period only the monthly plan is locked; Join class stays open.
- Tutor pay per class length (default 30 min ₦850, 45 min ₦1,275, same for every tutor), saved on each class when it is held. Tutors see their monthly total (starts again on the 1st). Tutor summary for supervisors, with CSV download.

**Everything else**
- Background jobs: class reminders to parents, the tutors' 6 AM class list, feedback reminders and supervisor alerts, fee reminders, carry-over notices.
- Announcements, break days, branding (name, logo, colours, contact, ID prefix), rules and timings (Super Admin), parent "Report an issue" buttons that open the supervisor's WhatsApp with the message written.
- Phone-first layout in the Quran Command style, Arabic and emoji support (utf8mb4).

## Run it

1. Install the .NET 10 SDK and MySQL 8. Install the EF tool once: `dotnet tool install --global dotnet-ef`
2. Create the database: `CREATE DATABASE quran_anchor CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;`
3. From `src/QuranAnchor.Presentation`, keep secrets out of the files:
   ```
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3306;Database=quran_anchor;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;"
   dotnet user-secrets set "Seed:SuperAdmin:Email" "you@example.com"
   dotnet user-secrets set "Seed:SuperAdmin:Password" "ChooseATempPassword1"
   dotnet user-secrets set "Seed:SuperAdmin:FullName" "Your full name"
   ```
4. From the solution folder, create the first migration:
   ```
   dotnet ef migrations add InitialCreate --project src/QuranAnchor.Infrastructure --startup-project src/QuranAnchor.Presentation --output-dir Persistence/Migrations
   ```
5. Run: `dotnet run --project src/QuranAnchor.Presentation` and open https://localhost:7080
   In Development the tables are created automatically, the five roles, default prices and tutor rates are added, and the first Super Admin is created from the seed settings. In production set `Database:AutoMigrate` to false and run `dotnet ef database update`.
6. Sign in, choose a password, set up two-step verification (scan the QR code), then open Branding, Pricing and Rules.

## Settings you will need

- `App:BaseUrl`: your site address (used in emails), for example `https://myqurananchor.com`.
- Email: while `Email:Provider` is `Logging`, emails are printed in the console (reset links, passwords, reminders). To send real email set `Email:Provider` to `Resend`, plus `Email:ApiKey` and `Email:FromAddress` (on your verified domain).
- Paystack: `Paystack:SecretKey` (use the test key first). In the Paystack dashboard set the webhook URL to `https://YOUR-SITE/Payments/Webhook`. Check that USD and GBP are enabled on your account.
- Arabic in the plan PDF: download **Amiri-Regular.ttf** (free, Google Fonts) and put it in `src/QuranAnchor.Presentation/wwwroot/fonts/`.
- Hosting: the app keeps sign-in keys in memory-based defaults. On a real server, configure ASP.NET Core Data Protection to persist keys, and put the site behind HTTPS.

## If the build or restore fails

- Packages use floating versions (`10.*`, QuestPDF `*`). If NuGet cannot find `Pomelo.EntityFrameworkCore.MySql` 10.x, check which version supports EF Core 10, or switch to `MySql.EntityFrameworkCore` (change the package in `QuranAnchor.Infrastructure.csproj` and the single `UseMySql(...)` call in `Infrastructure/DependencyInjection.cs`).
- If your MySQL is not 8.0.36, change `Database:ServerVersion` (for example `8.4.0-mysql`), or empty it to detect automatically.
- `QuranAnchor.slnx` needs Visual Studio 17.13 or newer. Otherwise run `dotnet new sln -n QuranAnchor` and `dotnet sln add` for the four projects.
- QuestPDF is free for organizations under USD 1 million a year; keep an eye on its license terms.

## A quick test path

1. Pricing page: check the three prices and the two tutor rates.
2. Create a tutor, then a student (give two weekly slots). Sign in as each (the temporary password is shown and emailed).
3. As the tutor: open a class that has started, write feedback, copy the WhatsApp text. Try "student absent" before and after the waiting time.
4. As the parent: request a reschedule; accept it as the tutor.
5. Write a plan, publish it, download the PDF. Open the monthly report, add remarks, publish, download the PDF.
6. Pay with a Paystack test card; confirm the receipt and the "paid until" date.
7. Replace tutor; check the old tutor cannot sign in and the checklist appears.

## Where things live

- `Domain`: entities, enums, roles, permissions, setting keys.
- `Application`: interfaces, rules (`SystemSettings`, `FeeRules`), report models, time helpers, role-to-permission map.
- `Infrastructure`: database context, Identity, services (scheduling, feedback, reports, PDFs, payments, accounts, notifications, background jobs), email senders.
- `Presentation`: controllers, views, styles, policies, filters.
