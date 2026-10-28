# Image Nation contract lifecycle

A local contract register for Image Nation Abu Dhabi. A request starts on the Contract Start Sheet from the 21 October 2026 proposal, then moves through legal triage, drafting, delegation-of-authority approval, signature, and post-execution dates.

The app is ASP.NET Core MVC on .NET 10. Every record is stored in PostgreSQL.

## Run it

You need the .NET 10 SDK and PostgreSQL.

```bash
sudo service postgresql start
sudo -u postgres psql -c "CREATE USER imagenation WITH PASSWORD 'imagenation' CREATEDB;"
sudo -u postgres psql -c "CREATE DATABASE imagenation_clm OWNER imagenation;"
dotnet tool restore
dotnet run --project src/ImageNation.Clm --urls http://127.0.0.1:47291
```

Open [http://127.0.0.1:47291](http://127.0.0.1:47291). The first launch applies the EF Core migration and loads demonstration titles, people, and agreements.

The development connection string is in `src/ImageNation.Clm/appsettings.json`:

`Host=localhost;Port=5432;Database=imagenation_clm;Username=imagenation;Password=imagenation;SSL Mode=Disable`

Uploaded drafts, trade licences, passports, and executed copies are files under `src/ImageNation.Clm/App_Data/uploads`. Their paths are stored in PostgreSQL.

## Personas

Sign-in is a persona picker. It stands in for Microsoft 365 single sign-on. There is no password.

| Person | Role | What they do |
| --- | --- | --- |
| Layla Hassan | Business requestor, Production | Raises a start sheet and updates obligations she owns |
| Omar Farouk | Legal triage | Accepts or returns a request and assigns a lawyer |
| Sara El-Masri | Counsel | Drafts, submits for approval, and runs signature |
| James Okonkwo | Counsel | His own queue |
| Nadia Qureshi | Line producer | Project approval below 50,000 USD |
| Helen Cho | CFO | First corporate approver at or above 50,000 USD |
| Rami Haddad | Head of Legal | Second corporate approver, workload, reassignment |
| Amira Al-Suwaidi | CEO | Exceptional route for an international co-production |
| Priya Shah | Administrator | Agreement types, non-supplier categories, threshold, rates, reminders |

A requestor sees agreements from their own department. A lawyer sees matters assigned to them, including ones they held before. An approver sees agreements where they have a decision. Legal triage, the Head of Legal, and the administrator see the portfolio.

## What the start sheet does

Department comes from the signed-in person and cannot be typed. VAT is 5% of the contract value. The same currency is shown on the value, the VAT, and the total. A renewal, amendment, extension, variation, or additional service must name the parent agreement.

The three reporting labels on the proposal — In Process, Fully Executed and Complete, and Closed/Not Proceeding — are derived from the lifecycle. A new request cannot be marked executed at intake.

## Approval

Value is converted to USD with the rates on the settings page. The seeded rate is 3.6725 AED per USD. Below 50,000 USD the route is the project approver. At or above that, it is the CFO and then the Head of Legal. An international co-production adds the CEO. Legal can insert an ad hoc approver, with a reason, while approval is open. A rejection unlocks the draft and keeps the approval package.

## What is simulated

Microsoft 365 sign-in, DocuSign, Adobe Sign, and Dynamics 365 Business Central are not connected. Signature progress is recorded by Legal in signing order. A paper signing is an upload of the executed copy, and the approved version stays in the history. The Business Central panel shows the metadata that would sync and can store a local marker. It does not call an API. Reminders appear on the dashboard. They are not emails.

## Layout

`src/ImageNation.Clm` is the MVC project. Domain types live in `Domain`, PostgreSQL mappings and the seed in `Data`, and the start sheet, routing, and lifecycle rules in `Services`.
