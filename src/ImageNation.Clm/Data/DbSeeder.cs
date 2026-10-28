using ImageNation.Clm.Domain;
using ImageNation.Clm.Services;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, FileStorage files)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var year = now.Year;

        AppUser User(string name, string email, UserRole role, string department, string title) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Role = role,
            Department = department,
            Title = title,
            IsActive = true
        };

        var layla = User("Layla Hassan", "layla.hassan@imagenation.demo", UserRole.Requestor, "Production", "Project Coordinator");
        var omar = User("Omar Farouk", "omar.farouk@imagenation.demo", UserRole.LegalTriage, "Legal", "Contract Administration");
        var sara = User("Sara El-Masri", "sara.elmasri@imagenation.demo", UserRole.Lawyer, "Legal", "Counsel");
        var james = User("James Okonkwo", "james.okonkwo@imagenation.demo", UserRole.Lawyer, "Legal", "Counsel");
        var nadia = User("Nadia Qureshi", "nadia.qureshi@imagenation.demo", UserRole.ProjectApprover, "Production", "Line Producer / Head of Production");
        var helen = User("Helen Cho", "helen.cho@imagenation.demo", UserRole.CorporateApprover, "Finance", "Chief Financial Officer");
        var rami = User("Rami Haddad", "rami.haddad@imagenation.demo", UserRole.HeadOfLegal, "Legal", "Head of Legal");
        var amira = User("Amira Al-Suwaidi", "amira.alsuwaidi@imagenation.demo", UserRole.Ceo, "Executive", "Chief Executive Officer");
        var priya = User("Priya Shah", "priya.shah@imagenation.demo", UserRole.Admin, "IT", "System Administrator");
        var users = new List<AppUser> { layla, omar, sara, james, nadia, helen, rami, amira, priya };
        db.Users.AddRange(users);

        InsEntity Ins(string name, int order) => new() { Id = Guid.NewGuid(), Name = name, SortOrder = order, IsActive = true };
        var studios = Ins("Image Nation Studios FZ LLC", 1);
        var cayman = Ins("Image Nation Cayman", 2);
        var feeder = Ins("Image Nation Cayman Feeder Ltd", 3);
        var us = Ins("Image Nation US Feeder", 4);
        var hyde = Ins("Hyde Park Image Nation", 5);
        var hydeSg = Ins("Hyde Park Image Nation Singapore", 6);
        var parkes = Ins("Parkes MacDonald Image Nation", 7);
        var fund = Ins("PM/Fund", 8);
        var splitter = Ins("IN Splitter", 9);
        var third = Ins("Third Party Entity", 10);
        db.InsEntities.AddRange(studios, cayman, feeder, us, hyde, hydeSg, parkes, fund, splitter, third);

        AgreementType Type(string name, int order) => new() { Id = Guid.NewGuid(), Name = name, SortOrder = order, IsActive = true };
        var talent = Type("Talent / crew", 1);
        var consultancy = Type("Consultancy", 2);
        var sla = Type("Service-level", 3);
        var distribution = Type("Distribution / licensing", 4);
        var copro = Type("Co-production", 5);
        var nda = Type("NDA", 6);
        db.AgreementTypes.AddRange(talent, consultancy, sla, distribution, copro, nda);

        NonSupplierCategory Cat(string name, int order) => new() { Id = Guid.NewGuid(), Name = name, SortOrder = order, IsActive = true };
        var cast = Cat("Talent / Cast", 1);
        var crew = Cat("Crew", 2);
        var consultant = Cat("Consultant", 3);
        var coproParty = Cat("Co-production partner", 4);
        var distributor = Cat("Distributor / Licensee", 5);
        var finance = Cat("Financing partner", 6);
        var government = Cat("Government / Authority", 7);
        var other = Cat("Other", 8);
        db.NonSupplierCategories.AddRange(cast, crew, consultant, coproParty, distributor, finance, government, other);

        var orchard = new ProjectTitle
        {
            Id = Guid.NewGuid(),
            Name = "The Desert Orchard",
            InternalCode = "DO-2026",
            ProductionEntity = studios.Name,
            Status = "In production",
            BusinessOwner = "Layla Hassan"
        };
        var gulf = new ProjectTitle
        {
            Id = Guid.NewGuid(),
            Name = "Gulf Current",
            InternalCode = "GC-2026",
            ProductionEntity = studios.Name,
            Status = "Development",
            BusinessOwner = "Layla Hassan"
        };
        var overhead = new ProjectTitle
        {
            Id = Guid.NewGuid(),
            Name = "Studio Overhead",
            InternalCode = "SO-2026",
            ProductionEntity = studios.Name,
            Status = "Active",
            BusinessOwner = "Rami Haddad"
        };
        db.Projects.AddRange(orchard, gulf, overhead);

        var policy = new DoaPolicy { Id = 1, ThresholdUsd = 50_000m };
        db.DoaPolicies.Add(policy);
        var rates = new List<FxRate>
        {
            new() { Currency = CurrencyCode.USD, UnitsPerUsd = 1m },
            new() { Currency = CurrencyCode.AED, UnitsPerUsd = 3.6725m },
            new() { Currency = CurrencyCode.EUR, UnitsPerUsd = 0.9200m },
            new() { Currency = CurrencyCode.GBP, UnitsPerUsd = 0.7900m }
        };
        db.FxRates.AddRange(rates);
        db.ReminderLeadDays.AddRange(new ReminderLeadDay { Days = 90 }, new ReminderLeadDay { Days = 60 }, new ReminderLeadDay { Days = 30 });
        db.NumberSequences.Add(new ContractNumberSequence { Year = year, LastNumber = 12 });

        Contract Make(
            int number,
            string name,
            AgreementType type,
            RequestNature nature,
            InitiationMethod method,
            string? template,
            InsEntity ins,
            ProjectTitle project,
            string owner,
            string party,
            string partyRef,
            PartyKind kind,
            NonSupplierCategory category,
            ResidenceRegion region,
            string place,
            string country,
            CurrencyCode currency,
            decimal net,
            bool coPro,
            LifecycleStatus status,
            AppUser initiator,
            AppUser? lawyer,
            int ageDays,
            string description,
            DateOnly? start,
            DateOnly? end,
            Contract? parent)
        {
            var created = now.AddDays(-ageDays);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                PublicId = $"IN-{year}-{number:0000}",
                ContractName = name,
                AgreementTypeId = type.Id,
                RequestNature = nature,
                InitiationMethod = method,
                TemplateName = template,
                Description = description,
                StartDate = start,
                EndDate = end,
                InsEntityId = ins.Id,
                Department = initiator.Department,
                ProjectTitleId = project.Id,
                ProjectOwner = owner,
                OtherPartyName = party,
                OtherPartyReference = partyRef,
                OtherPartyKind = kind,
                NonSupplierCategoryId = category.Id,
                Address = "As held on the party file",
                ResidenceRegion = region,
                SpecificPlace = place,
                Country = country,
                Currency = currency,
                ContractValueExVat = net,
                VatAmount = Money.VatOn(net),
                TotalValue = Money.Total(net),
                PaymentTerms = "30 days from a valid invoice.",
                PayerInvoicingEntity = ins.Name,
                PayeeInvoicingEntity = party,
                InternationalCoProduction = coPro,
                LifecycleStatus = status,
                DocumentLocked = Labels.LocksDocument(status),
                ParentContractId = parent?.Id,
                InitiatorId = initiator.Id,
                AssignedLawyerId = lawyer?.Id,
                CreatedAt = created,
                UpdatedAt = created.AddDays(Math.Min(2, ageDays))
            };
            db.Contracts.Add(contract);
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                Contract = contract,
                ActorId = initiator.Id,
                Action = "Request created",
                Detail = "Loaded with the demonstration register.",
                CreatedAt = created
            });
            if (lawyer != null)
            {
                db.Assignments.Add(new AssignmentEvent
                {
                    Id = Guid.NewGuid(),
                    Contract = contract,
                    ToLawyerId = lawyer.Id,
                    AssignedById = omar.Id,
                    Reason = "Initial allocation",
                    AssignedAt = created.AddHours(4)
                });
            }

            contract.RelatedDocuments.Add(new RelatedDocument
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                Kind = kind == PartyKind.Individual ? RelatedDocumentKind.Passport : RelatedDocumentKind.TradeLicence,
                FileName = kind == PartyKind.Individual ? "passport.txt" : "trade-licence.txt",
                StoredPath = "",
                UploadedById = initiator.Id,
                UploadedAt = created
            });
            return contract;
        }

        async Task AddVersion(Contract contract, int number, VersionKind kind, bool current, string fileName, string note, AppUser by, string body)
        {
            var stored = await files.SaveTextAsync(contract.Id, fileName, body);
            var doc = contract.RelatedDocuments.FirstOrDefault(d => d.StoredPath == "");
            if (doc != null)
            {
                var idDoc = await files.SaveTextAsync(contract.Id, doc.FileName, $"Specimen {doc.Kind} for {contract.OtherPartyName}. Demonstration file, not a real identity document.");
                doc.StoredPath = idDoc.RelativePath;
                doc.FileName = idDoc.FileName;
            }

            contract.Versions.Add(new DocumentVersion
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                VersionNumber = number,
                FileName = stored.FileName,
                StoredPath = stored.RelativePath,
                Kind = kind,
                IsCurrent = current,
                IsLocked = !current || contract.DocumentLocked,
                ChangeNote = note,
                CreatedById = by.Id,
                CreatedAt = contract.CreatedAt.AddDays(number)
            });
        }

        void AddRoute(Contract contract, bool complete)
        {
            var plan = DoaRouter.Plan(contract, policy, rates, users);
            contract.RouteExplanation = plan.Explanation;
            var sequence = 1;
            foreach (var step in plan.Steps)
            {
                contract.ApprovalSteps.Add(new ApprovalStep
                {
                    Id = Guid.NewGuid(),
                    ContractId = contract.Id,
                    Round = 1,
                    Sequence = sequence,
                    RouteKind = step.Kind,
                    ApproverId = step.Approver.Id,
                    Outcome = complete ? ApprovalOutcome.Approved : ApprovalOutcome.Pending,
                    IsActionable = !complete && sequence == 1,
                    Comment = complete ? "Approved." : null,
                    DecidedAt = complete ? contract.UpdatedAt : null,
                    RouteExplanation = step.Why
                });
                sequence++;
            }
        }

        var c1 = Make(1, "Lead casting — The Desert Orchard", talent, RequestNature.New, InitiationMethod.UploadedDraft, null, studios, orchard, "Layla Hassan", "Noor Al Hashimi", "CP-100001", PartyKind.Individual, cast, ResidenceRegion.Uae, "Abu Dhabi", "United Arab Emirates", CurrencyCode.AED, 80_000m, false, LifecycleStatus.Requested, layla, null, 1, "Lead role, principal photography in Al Ain.", today, today.AddMonths(8), null);
        await AddVersion(c1, 1, VersionKind.WorkingDraft, true, "casting-offer.txt", "Third-party draft received at intake.", layla, "Offer letter for the lead role. Working draft.");

        var c2 = Make(2, "Writers room consultancy — Gulf Current", consultancy, RequestNature.New, InitiationMethod.FromScratch, null, studios, gulf, "Layla Hassan", "Tide House Consulting LLC", "CP-100002", PartyKind.Company, consultant, ResidenceRegion.GccExceptUae, "Riyadh", "Saudi Arabia", CurrencyCode.AED, 45_000m, false, LifecycleStatus.LegalReview, layla, null, 3, "Twelve-week writers room. Fees exclusive of VAT.", today.AddDays(14), today.AddMonths(4), null);
        await AddVersion(c2, 1, VersionKind.WorkingDraft, true, "from-scratch.txt", "Blank controlled shell.", omar, "Controlled shell. Clause language still to be authored.");

        var c3 = Make(3, "Director of photography — The Desert Orchard", talent, RequestNature.New, InitiationMethod.Template, "Crew deal memo", studios, orchard, "Layla Hassan", "Karim Nassar", "CP-100003", PartyKind.Individual, crew, ResidenceRegion.Uae, "Dubai", "United Arab Emirates", CurrencyCode.AED, 120_000m, false, LifecycleStatus.Drafting, layla, sara, 12, "Prep, shoot, and grade. Equipment supplied by production.", today.AddDays(-10), today.AddMonths(6), null);
        await AddVersion(c3, 1, VersionKind.Historical, false, "dop-v1.txt", "Opened from the crew deal memo.", sara, "Version 1. Fee still marked as subject to schedule.");
        await AddVersion(c3, 2, VersionKind.WorkingDraft, true, "dop-v2.txt", "Shoot weeks corrected. Prep week added.", sara, "Version 2. Prep week inserted. Historical copy kept.");

        var c4 = Make(4, "MENA distribution licence — Gulf Current", distribution, RequestNature.New, InitiationMethod.Template, "Distribution licence", cayman, gulf, "Layla Hassan", "Northwind Screens Ltd", "CP-100004", PartyKind.Company, distributor, ResidenceRegion.OutsideGcc, "London", "United Kingdom", CurrencyCode.USD, 250_000m, false, LifecycleStatus.AwaitingApproval, layla, james, 9, "Exclusive MENA television and SVOD, five years.", today, today.AddYears(5), null);
        await AddVersion(c4, 1, VersionKind.WorkingDraft, true, "distribution-v1.txt", "Commercial draft locked for corporate approval.", james, "Licence draft submitted for DOA.");
        AddRoute(c4, complete: false);

        var c5 = Make(5, "UK co-production — The Desert Orchard", copro, RequestNature.New, InitiationMethod.UploadedDraft, null, studios, orchard, "Layla Hassan", "Thames & Dune Pictures Ltd", "CP-100005", PartyKind.Company, coproParty, ResidenceRegion.OutsideGcc, "London", "United Kingdom", CurrencyCode.AED, 120_000m, true, LifecycleStatus.AwaitingApproval, layla, sara, 6, "International co-production. Exceptional route applies.", today.AddMonths(1), today.AddMonths(18), null);
        await AddVersion(c5, 1, VersionKind.WorkingDraft, true, "copro-v1.txt", "Counterparty draft. Co-production flag set.", sara, "Co-production draft.");
        AddRoute(c5, complete: false);

        var c6 = Make(6, "Mutual NDA — location scout", nda, RequestNature.New, InitiationMethod.Template, "Mutual NDA", studios, overhead, "Omar Farouk", "Liwa Locations LLC", "CP-100006", PartyKind.Company, other, ResidenceRegion.Uae, "Abu Dhabi", "United Arab Emirates", CurrencyCode.AED, 5_000m, false, LifecycleStatus.PartiallySigned, omar, james, 15, "Location access ahead of a permit.", today.AddDays(-20), today.AddYears(2), null);
        await AddVersion(c6, 1, VersionKind.ApprovedPackage, true, "nda-approved.txt", "Approved NDA out for signature.", james, "Mutual NDA approved package.");
        AddRoute(c6, complete: true);
        c6.Signatories.Add(new Signatory { Id = Guid.NewGuid(), ContractId = c6.Id, Round = 1, SigningOrder = 1, Name = "Huda Al Kaabi", Organization = "Liwa Locations LLC", State = SignatureState.Signed, SentAt = now.AddDays(-2), SignedAt = now.AddDays(-1) });
        c6.Signatories.Add(new Signatory { Id = Guid.NewGuid(), ContractId = c6.Id, Round = 1, SigningOrder = 2, Name = "Rami Haddad", Organization = "Image Nation Studios FZ LLC", State = SignatureState.Pending, SentAt = now.AddDays(-2) });

        var c7 = Make(7, "Supporting cast cluster — The Desert Orchard", talent, RequestNature.New, InitiationMethod.Template, "Talent engagement", studios, orchard, "Layla Hassan", "Orchard Cast Collective", "CP-100007", PartyKind.Company, cast, ResidenceRegion.Uae, "Abu Dhabi", "United Arab Emirates", CurrencyCode.AED, 150_000m, false, LifecycleStatus.ExecutedActive, layla, james, 40, "Five supporting roles. Executed copy is authoritative.", today.AddDays(-30), today.AddDays(400), null);
        await AddVersion(c7, 1, VersionKind.ApprovedPackage, false, "cast-approved.txt", "Approved package retained.", james, "Approved cast agreement.");
        await AddVersion(c7, 2, VersionKind.ExecutedCopy, true, "cast-executed.txt", "Executed copy.", james, "Signed by all parties.");
        AddRoute(c7, complete: true);
        c7.ExecutionDate = today.AddDays(-20);
        c7.ExecutionSignatories = "Layla Hassan; Orchard Cast Collective";
        c7.BcLastSyncAt = now.AddDays(-18);
        c7.Obligations.Add(new Obligation { Id = Guid.NewGuid(), ContractId = c7.Id, Description = "Locked Cut delivered to the distributor", MilestoneKind = MilestoneKind.LockedCut, OwnerId = layla.Id, DueDate = today.AddDays(10), Status = ObligationStatus.Open, IsMandatory = true });
        c7.Obligations.Add(new Obligation { Id = Guid.NewGuid(), ContractId = c7.Id, Description = "Final Sound Mix", MilestoneKind = MilestoneKind.FinalSoundMix, OwnerId = layla.Id, DueDate = today.AddDays(70), Status = ObligationStatus.Open, IsMandatory = true });
        c7.Obligations.Add(new Obligation { Id = Guid.NewGuid(), ContractId = c7.Id, Description = "First rehearsal call sheet issued", MilestoneKind = MilestoneKind.General, OwnerId = layla.Id, DueDate = today.AddDays(-15), Status = ObligationStatus.Completed, IsMandatory = true, EvidenceNote = "Call sheet filed with production.", CompletedAt = now.AddDays(-14) });
        c7.Rights.Add(new RightsWindow { Id = Guid.NewGuid(), ContractId = c7.Id, RightType = "All media excluding airline", Territory = "MENA", WindowStart = today.AddDays(-300), WindowEnd = today.AddDays(40), Exclusivity = "Exclusive", Counterparty = "Orchard Cast Collective" });
        c7.Rights.Add(new RightsWindow { Id = Guid.NewGuid(), ContractId = c7.Id, RightType = "SVOD", Territory = "Worldwide excluding MENA", WindowStart = today.AddDays(40), WindowEnd = today.AddDays(400), Exclusivity = "Non-exclusive", Counterparty = "Orchard Cast Collective" });
        c7.CriticalDates.Add(new CriticalDate { Id = Guid.NewGuid(), ContractId = c7.Id, Kind = CriticalDateKind.Expiry, Date = today.AddDays(400), Note = "End of the engagement term." });
        c7.CriticalDates.Add(new CriticalDate { Id = Guid.NewGuid(), ContractId = c7.Id, Kind = CriticalDateKind.NoticeDeadline, Date = today.AddDays(340), Note = "Notice if the option is not to be taken." });

        var c8 = Make(8, "Post house services — Gulf Current", sla, RequestNature.New, InitiationMethod.Template, "Services agreement", studios, gulf, "Layla Hassan", "Harbour Finish LLC", "CP-100008", PartyKind.Company, other, ResidenceRegion.Uae, "Abu Dhabi", "United Arab Emirates", CurrencyCode.USD, 90_000m, false, LifecycleStatus.RenewalReview, layla, sara, 50, "Picture finish. Auto-renews unless notice is served.", today.AddMonths(-10), today.AddDays(25), null);
        await AddVersion(c8, 1, VersionKind.ExecutedCopy, true, "post-executed.txt", "Executed services agreement.", sara, "Executed services agreement.");
        AddRoute(c8, complete: true);
        c8.ExecutionDate = today.AddMonths(-10);
        c8.ExecutionSignatories = "Sara El-Masri; Harbour Finish LLC";
        c8.CriticalDates.Add(new CriticalDate { Id = Guid.NewGuid(), ContractId = c8.Id, Kind = CriticalDateKind.NoticeDeadline, Date = today.AddDays(18), Note = "Last day to refuse renewal." });
        c8.CriticalDates.Add(new CriticalDate { Id = Guid.NewGuid(), ContractId = c8.Id, Kind = CriticalDateKind.OptionExercise, Date = today.AddDays(25), Note = "Option for a second series block." });
        c8.CriticalDates.Add(new CriticalDate { Id = Guid.NewGuid(), ContractId = c8.Id, Kind = CriticalDateKind.AutoRenewal, Date = today.AddDays(25), Note = "Renews for twelve months if notice is not served." });

        var c9 = Make(9, "Archive research consultancy", consultancy, RequestNature.New, InitiationMethod.FromScratch, null, studios, overhead, "Layla Hassan", "Paper Minaret", "CP-100009", PartyKind.Individual, consultant, ResidenceRegion.Uae, "Sharjah", "United Arab Emirates", CurrencyCode.AED, 12_000m, false, LifecycleStatus.Closed, layla, null, 20, "Not proceeding. The researcher withdrew.", null, null, null);
        await AddVersion(c9, 1, VersionKind.Historical, true, "research-shell.txt", "Shell only. Matter closed before drafting.", layla, "Unfinished shell.");
        c9.ClosureOverrideReason = "Counterparty withdrew before drafting.";

        var c10 = Make(10, "Cast cluster amendment — additional rehearsal week", talent, RequestNature.Amendment, InitiationMethod.FromScratch, null, studios, orchard, "Layla Hassan", "Orchard Cast Collective", "CP-100007", PartyKind.Company, cast, ResidenceRegion.Uae, "Abu Dhabi", "United Arab Emirates", CurrencyCode.AED, 15_000m, false, LifecycleStatus.Drafting, layla, sara, 4, "One extra rehearsal week. Linked to the executed cast agreement.", today.AddDays(7), today.AddDays(30), c7);
        await AddVersion(c10, 1, VersionKind.WorkingDraft, true, "amendment-v1.txt", "Amendment shell linked to the parent agreement.", sara, "Amendment for an additional rehearsal week.");

        var c11 = Make(11, "Camera department — Gulf Current pilot", talent, RequestNature.New, InitiationMethod.Template, "Crew deal memo", studios, gulf, "Layla Hassan", "Focus Pull Guild", "CP-100011", PartyKind.Company, crew, ResidenceRegion.Uae, "Abu Dhabi", "United Arab Emirates", CurrencyCode.AED, 30_000m, false, LifecycleStatus.ExecutedActive, layla, james, 25, "Pilot week. A delivery obligation is overdue.", today.AddDays(-20), today.AddDays(60), null);
        await AddVersion(c11, 1, VersionKind.ExecutedCopy, true, "camera-executed.txt", "Executed crew agreement.", james, "Executed crew agreement.");
        AddRoute(c11, complete: true);
        c11.ExecutionDate = today.AddDays(-18);
        c11.ExecutionSignatories = "James Okonkwo; Focus Pull Guild";
        c11.Obligations.Add(new Obligation { Id = Guid.NewGuid(), ContractId = c11.Id, Description = "Camera reports delivered to the production office", MilestoneKind = MilestoneKind.General, OwnerId = layla.Id, DueDate = today.AddDays(-4), Status = ObligationStatus.Open, IsMandatory = true });

        var c12 = Make(12, "Airline window — The Desert Orchard", distribution, RequestNature.New, InitiationMethod.UploadedDraft, null, hyde, orchard, "Layla Hassan", "Cedar Air Entertainment", "CP-100012", PartyKind.Company, distributor, ResidenceRegion.OutsideGcc, "Doha", "Qatar", CurrencyCode.USD, 60_000m, false, LifecycleStatus.AwaitingSignature, layla, sara, 11, "Six-month airline window. Approved, not yet signed.", today.AddDays(20), today.AddMonths(8), null);
        await AddVersion(c12, 1, VersionKind.ApprovedPackage, true, "airline-approved.txt", "Approved package sent for signature.", sara, "Approved airline window.");
        AddRoute(c12, complete: true);
        c12.Signatories.Add(new Signatory { Id = Guid.NewGuid(), ContractId = c12.Id, Round = 1, SigningOrder = 1, Name = "Cedar Air Entertainment", Organization = "Cedar Air Entertainment", State = SignatureState.Pending, SentAt = now.AddDays(-1) });
        c12.Signatories.Add(new Signatory { Id = Guid.NewGuid(), ContractId = c12.Id, Round = 1, SigningOrder = 2, Name = "Rami Haddad", Organization = "Hyde Park Image Nation", State = SignatureState.Pending, SentAt = now.AddDays(-1) });

        db.Notices.Add(new Notice { Id = Guid.NewGuid(), UserId = nadia.Id, ContractId = c5.Id, Message = $"{c5.PublicId} is waiting for your approval.", CreatedAt = now.AddHours(-6) });
        db.Notices.Add(new Notice { Id = Guid.NewGuid(), UserId = helen.Id, ContractId = c4.Id, Message = $"{c4.PublicId} is waiting for your approval.", CreatedAt = now.AddHours(-5) });
        db.Notices.Add(new Notice { Id = Guid.NewGuid(), UserId = sara.Id, ContractId = c3.Id, Message = $"{c3.PublicId} is in your queue.", CreatedAt = now.AddDays(-2), IsRead = true });

        await db.SaveChangesAsync();
    }
}
