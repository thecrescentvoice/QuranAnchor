Quran Anchor Portal: Product Requirements Document
Prepared for Okunlola Abdul-Azeez Olanrewaju · Version 1.0 · 17 October 2026
1. Overview
Product. A web portal for running a one-on-one online Quran academy. Working name: Quran Anchor. The Super Admin can rename it later, so the name must never be written into the code.
Purpose. Let the academy schedule classes, join them on Google Meet, record feedback after every class, produce monthly plans and reports, collect a monthly portal fee online, and track tutor earnings, all in one place.
Launch target. Late December 2026 or early January 2027. The 12-week plan in section 11 lands in early January.
Users. Students (many of them abroad) and their parents, tutors, a supervisor, an admin and a super admin.
Scope boundary. Only the monthly portal fee is paid in the portal. Class fees stay outside it: reminders and the account number are sent in the academy’s WhatsApp group.
2. Goals and non-goals
Goals
• One place for schedules, Meet links, feedback, plans and reports.
• Monthly reports built automatically from class feedback, with very little extra writing by tutors.
• Online collection of the portal fee in naira, dollars or pounds.
• Safe account control by the supervisor, including a clean handover when a tutor leaves.
• Phone-first, light on slow connections, with full Arabic and emoji support.
Non-goals for launch
• Taking class fees inside the portal.
• Group classes.
• Separate parent accounts (the parent uses the child’s login).
• Automatic WhatsApp messages.
• Voice-note uploads.
• Curriculum tracking (progress is based on class feedback).
• Built-in video (classes run on Google Meet).
3. Users and roles
• Super Admin. The owner. Can do everything. Manages admins, branding, portal prices, tutor pay rates and report thresholds. The only role that can permanently delete an account. Views the audit log.
• Admin (also acts as moderator). Runs the academy and checks quality. Can do everything the Supervisor can, plus view reports and the payments list, post announcements, set break days, and see the missing-feedback and package-ending lists. Manages supervisors. There is no separate moderator role.
• Supervisor. Day-to-day operations. Creates, edits, deactivates and reactivates student and tutor accounts, assigns tutors, sets sessions per month, uses Replace tutor and Rotate link, marks a student fee exempt or paid manually, and receives alerts.
• Tutor. Sees their own students, calendar and earnings. Edits a student’s weekly days and times after agreeing with the parent, marks attendance, writes feedback, plans and report remarks, and sends or answers reschedule requests.
• Student / parent. One login per student. For children, the parent holds and uses the login. Sees the schedule, Join class, feedback, plan, report and progress charts, pays the portal fee, requests reschedules and reports issues.
Hierarchy rules. A person can only manage roles below their own. The last Super Admin cannot be removed or demoted. Super Admin and Admin accounts use two-step verification.
4. Core business rules
1. Classes are one-on-one. Each student has one current tutor.
2. Each student chooses how many sessions they want per month (for example 9, 12 or 20). The supervisor sets this on the profile.
3. Packages, reports and tutor earnings follow calendar months. A student who joins mid-month gets a first package that runs to the end of that month, with the number of sessions set by the supervisor.
4. Sessions left at the end of a month carry over into the next package (for example 12 + 3 carried over = 15). The parent and tutor agree how those classes will be held.
5. A student’s weekly days and times repeat every week. A change happens only if the parent asks for one. After the parent and tutor agree, the tutor edits the day and time and picks the date it starts from. The parent is emailed and the change is logged.
6. A single class moves only if the student or parent and the tutor both agree in the portal (request, then accept). A moved class counts once toward the package.
7. Absence rule (a setting). A student absent without notice uses up one session. An excused absence, or a class cancelled by the tutor, does not.
8. A tutor can mark a student absent only after a waiting time (default 10 minutes). The parent is emailed when this happens.
9. A tutor earns only for classes actually held. A student no-show earns the tutor nothing.
10. The portal fee is ₦2,000, $1.50 or £1.15 per month. The student chooses the currency. It runs for one month from the payment date.
11. If the fee is overdue after a grace period (default 7 days), only the monthly plan is locked. Join class always stays available.
12. Accounts are deactivated, never deleted by default. Deactivated accounts can be reactivated. Only the Super Admin can delete permanently.
13. Class fees are handled outside the portal in the academy’s WhatsApp group.
5. Functional requirements
5.1 Accounts and access
• Login. Students sign in with their Student ID and a password. Staff sign in with their email and a password.
• IDs. Students look like QA-STU-0007 and tutors like QA-TUT-0003. The prefix is its own setting. If the academy name or prefix changes, existing IDs never change.
• Create student. One guided form: student details and the parent’s email, level (Beginner, Intermediate or Advanced), tutor, sessions per month, weekly days and times, the Meet link, then send the login email. The student gets a temporary password and must change it on first login.
• Forgot password. A link by email that works once and expires after an hour. The same message appears whether or not the email exists. For students, the email goes to the parent’s address on the student record.
• Deactivate and reactivate. A Deactivated accounts page lists them, with a Reactivate button for Supervisor, Admin and Super Admin.
• Replace tutor. Deactivate the leaving tutor, choose or create the new one, and move that tutor’s students and classes in one step. History stays intact. A checklist reminds the supervisor to rotate each affected student’s Meet link.
• Privacy notice and consent. Shown at first login with a tick box. The portal records who agreed, when and which version, and asks again when the notice changes.
• Level. Supervisor, Admin and Tutor can change a student’s level. Every change is recorded.
• Security. Lockout after repeated wrong passwords, idle session expiry, HTTPS everywhere, and an audit log of sensitive actions (account changes, rate and price changes, link changes, replace tutor, deletions).
5.2 Scheduling and sessions
• Weekly schedule. Each student has one or more weekly slots: day, local start time, time zone and duration (for example 30 or 45 minutes).
• Session generation. The portal creates sessions about four weeks ahead from the weekly slots and stops at the package total. Sessions are stored in UTC and shown in each person’s own time zone. When setting a time, both time zones are shown (for example 6:30 PM Lagos = 5:30 PM London).
• Editing the schedule. Changes apply from a chosen date. Past sessions are not touched. The parent is emailed.
• Add a session. A tutor can add a single extra class (for example to use up carried-over sessions) without changing the weekly pattern.
• Reschedule one class. Either side sends a request with a proposed time and a reason. The other side accepts, declines or proposes another time. The class moves only after acceptance. The supervisor can see and override requests.
• Double-booking check. The portal blocks a change that overlaps another class for the same tutor and shows the conflict. The supervisor can override.
• Break days. The Admin sets dates such as Eid. No classes are created on those days, and affected sessions carry over.
• Student pause. A student can be put on hold with an optional return date. Classes and fee reminders stop until then.
• Class not held. The tutor can mark a class not held with a reason: student absent, tutor unavailable, technical problem or other.
5.3 Packages and session counts
• A package is created automatically each month from the student’s sessions-per-month number, plus any carried-in sessions.
• The count shown on feedback and reports reads like 5/15, with a note such as 12 for the month + 3 carried over.
• The Supervisor and Tutor are reminded when sessions are still unscheduled late in the month, and when a package is nearly used up.
• At month end, any sessions left are carried over and the tutor and supervisor are prompted to agree how they will be held with the parent.
5.4 Google Meet
• Each student has one permanent Meet link on their profile, created from the academy’s Google account (a regular Gmail account) as a recurring Calendar event. When editing that event, always choose All events so the link never changes.
• Access is set to open (anyone with the link can join), and the setting that makes the host join first is off, so a tutor can start the class without the academy account being signed in.
• Admin and Supervisor can see, edit and Rotate the link. Every change is logged with a date.
• Tutors and students see only a Join class button, active from about 10 minutes before the class until it ends.
• Rotate the link whenever a tutor leaves or a link may have been shared. Tutors are told to check who is in the call at the start and to end the call and report any unknown person.
• Test before launch (week 1): an invited tutor and student can join directly, a tutor can start the class with the academy account signed out, and the open setting behaves as expected.
5.5 Class feedback
The tutor completes one feedback form per class, following the academy’s existing format. Most fields are toggles or dropdowns with sensible defaults, so it takes about a minute.
• Filled in automatically: student name, date, session count, time used (editable).
• Today’s lesson, and the learning objective (filled from the monthly plan, editable).
• Goal achieved: Yes, Partially or No. Partially and No show an optional note for what is left or why.
• Student on track with the lesson, and student tasks progress (choices plus an optional comment).
• Student joined on time or late.
• Teacher and student camera (on or off), environment (conducive or not) and network (stable or not).
• Daily task given and daily task submitted (voice note and written). The class Task text and whether it was submitted.
• Lesson slides link, worksheet, and the teacher’s note.
• Copy as WhatsApp message produces the same format with emojis (for example Yes ✓, Partially ◐, No ✗) so the current WhatsApp routine can continue.
• After the feedback is submitted, the amount earned for that class appears for the tutor.
• Missing feedback. If a class ends with no feedback and no Class not held mark, the tutor gets a reminder (default 1 hour after) and the supervisor is alerted (default 24 hours after). Both timings are settings. The Admin sees a Missing feedback list.
5.6 Monthly plan
• One plan per student per month, written by the tutor and published when ready. Students see only published plans.
• Contents: student, month and goal; post-class expectations; month goals; a weekly table of session numbers, content, activity and muraja’ah; daily tilawah focus by day range; notes for parents (student expectations, class format, home support); and a monthly report overview.
• Repeated text comes from an editable template, so the tutor writes only the student-specific parts. The class format line (for example 30-minute sessions on Tuesday and Saturday) is filled in from the schedule. A Copy last month’s plan button saves time.
• Each weekly row becomes the learning objective for those sessions in the feedback form.
• Downloadable PDF in the layout of the academy’s sample plan, including the Arabic bismillah, with an embedded Arabic font.
• Locked for a student whose portal fee is overdue past the grace period.
5.7 Monthly report
One report per student per calendar month, calculated from class records, with a short tutor-written part. Students see only published reports. Downloadable PDF.
• Header: student, student ID, level, tutor, reporting month, sessions held and scheduled.
• Headline rates: class delivery, student punctuality, on-track rate, assignment completion, daily task compliance, goal achievement (Partially counts as half).
• Overall classification averaged from the available measures only; anything not measured is left out, not counted as zero. The thresholds are settings.
• Session record, topics covered by date, goals and on-track summary, learning habits.
• Teacher scorecard: class delivery (held out of scheduled, leaving out student absences and agreed reschedules), lesson delivery, resource use, student engagement, and feedback quality (a note counts as substantive above a minimum length, which is a setting).
• Technical and environment issues taken from the camera, environment and network entries.
• Written by the tutor: focus for next month, a closing remark and a note for parents. The portal can draft the parent note from the data for the tutor to edit.
• Performance bars are drawn directly in the PDF.
5.8 Progress charts
Built from feedback data, session by session, and shown to the student, the tutor and the Admin.
• Package progress (for example 5/15).
• Goal trend: one marker per class coloured for Yes, Partially or No.
• Consistency: attendance and daily task submission by month.
• On-track rate by month.
• The Admin can filter for students with repeated Partially or No results.
5.9 Portal fee and payments
• Price table in Super Admin settings: ₦2,000, $1.50 and £1.15 per month. The student picks the currency at checkout, defaulting to their country. Nigerians living abroad can pay in naira with a Nigerian card or bank transfer.
• Provider: Paystack first, behind a payment interface (IPaymentGateway) so Flutterwave or another provider can be added without changing the rest of the portal.
• The amount is set by the server. The portal trusts only Paystack’s confirmation message (webhook) and checks that the amount and currency match the price table. Each payment reference is unique so a repeated message never credits twice.
• On success the student’s access date moves forward one month, and a receipt is available.
• Fee reminders go out by email before the due date. A fee-exempt switch covers scholarship cases. Supervisor and Admin can mark a payment manually (for example a transfer made outside the portal).
• Overdue rule: after the grace period, only the monthly plan is locked. Join class stays available.
• Launch is manual monthly payment with reminders. Automatic card renewal comes later.
• Fees to expect (verify when onboarding): roughly 1.5% + ₦100 for local cards, with the ₦100 waived under ₦2,500, and about 3.9% + ₦100 for international cards.
5.10 Tutor earnings and monthly summary
• Super Admin sets a rate for each class length, the same for every tutor for now: 30 minutes = ₦850 and 45 minutes = ₦1,275. A rate row can optionally be tied to one tutor later.
• The booked class length decides the rate. Only held classes earn. The amount is saved on the class when it is completed, so a later rate change never alters past months.
• The tutor sees the amount after each class, a running monthly total and a breakdown by date and student. The total restarts on the 1st of each month and past months stay in history.
• The tutor monthly summary shows students taught, sessions held, sessions not held with reasons, hours taught, feedback submitted versus missing, and the amount. Supervisor, Admin and Super Admin see all tutors. Each tutor sees only their own. It can be downloaded.
5.11 Notifications (email and in-portal)
• Account created, password reset, and the privacy notice.
• Class reminder to the parent 1 hour before each class. A morning email to each tutor listing that day’s classes (one email, not one per class).
• New or changed weekly schedule, and reschedule requests and answers.
• Student marked absent, sent to the parent.
• Missing feedback to the tutor, and the supervisor alerts described in 5.5.
• Package nearly used up, sessions unscheduled, and sessions left at month end.
• Portal fee due, payment receipt, and fee overdue.
• Plan or report published, and announcements.
• Automatic WhatsApp messages are not part of launch.
5.12 Issue reporting for parents
• A Report an issue button on the dashboard and on class and feedback pages, plus a Need help link on the login page.
• The parent chooses a category (class problem, concern about the tutor, payment, technical, other). The button opens the supervisor’s WhatsApp chat with a message already written, including the student’s name and ID, and the class details when started from a class page. The parent can edit it before sending.
• Built as a standard WhatsApp click-to-chat link. The number is a Super Admin setting in international format. A dedicated academy WhatsApp number is recommended over a personal one.
• Reports live in WhatsApp, so the portal does not store them. Tutors do not get this button because they use a WhatsApp group with the supervisor.
5.13 Announcements
Super Admin, Admin and Supervisor can post announcements to everyone, to tutors only or to students only. They appear on the dashboard and can also be emailed.
5.14 Branding and settings (Super Admin)
• Academy name, short name, logo, tagline, colours, contact email, supervisor WhatsApp number and ID prefix. Every page, email, PDF and the copy-to-WhatsApp text read these, so a rename needs no code change. The default is Quran Anchor.
• Other settings: portal prices, session pay rates, grace period, absence rule, waiting time for no-shows, reminder timings, report thresholds, minimum feedback length, and the plan and parent-note templates.
• Changes take effect immediately and are logged.
• A rename does not change the email domain, the Paystack business name or the Google account name. Those are changed separately.
6. Look and feel
• Style follows the Quran Command logo: a violet, cyan and magenta gradient with dark slate lettering. The Quran Anchor logo uses the monogram QA. (A placeholder is used until the final logo is ready.)
• Gradient only on the login page, a thin header strip, PDF top bands and the logo. Everyday screens use white or very light backgrounds with dark slate text.
• Suggested colours (approximate, to be confirmed from the original logo file): violet #8B5CFF, cyan #5CE1E6, magenta #D61F9F, slate #3E4C4F, solid buttons around #5B3FD9.
• Status colours are reserved for meaning: green #2E7D5B for yes or on track, amber #D98E04 for partially, red #C0392B for no or behind, grey #6B7280 for not recorded. Magenta is never used for status.
• Phone-first: large tap targets, tables that become cards, the Join button front and centre, light pages, and the option to add it to a phone’s home screen.
7. Data model
• User. Id, email or student code, password hash, role, active flag, must-change-password flag, consent version and time, created and deactivated dates.
• StudentProfile. User, student code, full name, country, time zone, phone, parent name, level, sessions per month, meeting link, preferred currency, fee-exempt flag, paid-until date, status (active, on hold, deactivated).
• TutorProfile. User, tutor code, full name, phone, status.
• TutorAssignment. Student, tutor, start and end dates (history).
• ClassSchedule. Student, tutor, day of week, local start time, time zone, duration, effective from and to.
• SessionPackage. Student, month, planned sessions, carried-in sessions, status.
• ClassSession. Package, student, tutor, start (UTC), duration, status (scheduled, completed, not held, rescheduled, cancelled), not-held reason, attendance, amount earned.
• RescheduleRequest. Session, requested by, proposed time, reason, status, responded by and time.
• SessionFeedback. Session, lesson, learning objective, goal achieved and note, time used, on track, task progress, student on time, camera, environment and network entries, daily task given and submitted, task, slides, worksheet, teacher note.
• MonthlyPlan and PlanRow. Package, goal, month goals, expectations, parent notes, tilawah focus, status; rows with session range, content, activity, muraja’ah.
• MonthlyReport. Student, month, tutor remark, focus for next month, parent note, status, published date.
• PortalPrice. Currency, amount, active.
• Payment. Student, provider, unique provider reference, currency, amount, status, paid date, period start and end.
• SessionRate. Duration, amount, optional tutor.
• BreakDay. Start, end, reason.
• Announcement, Setting, AuditLog.
8. Non-functional requirements
• Phones. Fully usable on a small screen and on slow data.
• Arabic and emoji. The database uses utf8mb4. Arabic text displays and prints correctly, including in PDFs with an embedded Arabic font. The WhatsApp format keeps its emojis.
• Time. Sessions in UTC, schedules in local time with a time zone ID so daylight saving changes never shift a class.
• Security. Role-based permissions, two-step verification for Super Admin and Admin, account lockout, idle expiry, HTTPS, verified payment messages, full audit log.
• Privacy. Plain-language notice and parent consent, recorded. Many students are children and some live abroad, so have the notice checked against Nigeria’s Data Protection Act 2023 and any rules that apply to families in other countries.
• Backups. Automatic daily database backups and a tested restore before launch.
• Reliability. Error logging and an uptime alert.
9. Technical approach
• ASP.NET Core with Razor pages and Bootstrap, EF Core and MySQL, built with Clean Architecture (Domain, Application, Infrastructure, Presentation).
• Background jobs (for example a hosted service or Hangfire) for session generation, reminders and alerts.
• Chart.js for charts. QuestPDF for the plan and report PDFs (free for organizations under USD 1 million in annual revenue).
• Paystack behind IPaymentGateway. An email provider with a free tier (for example Resend or Brevo) on a verified domain.
• Email limits to watch: the free tiers cap daily and monthly sending. The tutor morning digest keeps volume low. Move to a paid tier if the academy grows past the limit.
10. Success measures (suggested)
• Feedback submitted for at least 95% of held classes.
• Monthly plans and reports published by the 3rd of each month.
• No class lost to a wrong link or time zone mix-up.
• Parents paying the portal fee on time without a supervisor chasing them.
11. Build plan (12 weeks, landing in early January)
1. Setup, login with forgot password, roles and permissions, audit log, branding settings, Arabic and emoji support. Apply for the Paystack account. Run the Meet test.
2. Accounts: the guided create-student form, tutors, deactivate, reactivate and delete rules, Replace tutor, Rotate link, consent on first login, student IDs.
3. Weekly schedules, session generation, packages and carry-over, double-booking check, break days and student pause.
4. Phone-first dashboards and calendars, Join class, time zones, and the parent Report an issue button.
5. The feedback form and missing-feedback notifications.
6. Rescheduling and email reminders.
7. Paystack payments, price table and the overdue-fee lock.
8. Monthly plan and plan PDF.
9. Monthly report calculations, tutor earnings and the tutor monthly summary.
10. Report PDF and progress charts.
11. Admin dashboard, testing (time zones, phones, payments), backup and restore test.
12. Deployment and launch with the first students.
If time slips, the progress charts, the tutor earnings view and PDF polish can follow launch without breaking anything. Classes, feedback, payments, and the plan and report themselves cannot.
12. Launch and rollout
• The first students are the pilot group. Start small, fix problems as they appear, and open to more students as it settles.
• Class fee reminders and payments continue in the WhatsApp group.
• Ramadan 2027 is expected to begin around early February (the exact day depends on moon sighting). Class times often change then, so break days, student pause and schedule editing will be used straight away.
13. Risks and open items
• Paystack approval. Check early what they require, including whether the academy must be a registered business, whether USD and GBP collection are enabled, and any minimum amount for small payments.
• Domain and hosting. Not yet chosen. Email sending, payments and the login page all depend on them.
• Name and branding. Check that Quran Anchor, its domain and social handles are free (and the Corporate Affairs Commission name if registering). Replace the placeholder logo and confirm the colours from the original file.
• Open Meet links. Accepted trade-off: anyone with a link can join, so links are permanent but rotated whenever a tutor leaves or a link may be shared.
• Email delivery. Reminders can land in spam unless the domain is verified. Free sending limits may need a paid tier later.
• Privacy notice. Needs a plain-language draft and a legal read before launch.
• Scope. This is a large build for 12 weeks. Protect the core before the extras.
14. Assumptions to confirm
• “Task” and “Daily task” stay as two separate items.
• Partially counts as half a goal in the report.
• Class delivery is held divided by scheduled, leaving out student absences and agreed reschedules.
• Tutor pay uses the booked class length, not the minutes typed on the form.
• The overdue lock covers only the monthly plan.
• The no-show waiting time defaults to 10 minutes, the grace period to 7 days, and the feedback reminders to 1 hour and 24 hours.
15. After launch (backlog)
Automatic card renewal for the portal fee, automatic WhatsApp messages, voice-note uploads, completion certificates, an enquiry form for new students, Hijri dates, per-tutor pay rates, separate parent accounts, automatic Calendar invites through the Google Calendar API, bonuses, deductions and a paid tick for tutor earnings, and exports to spreadsheets.
