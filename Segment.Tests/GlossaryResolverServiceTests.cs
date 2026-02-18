using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class GlossaryResolverServiceTests : IDisposable
    {
        private readonly string _basePath;
        private readonly GlossaryResolverService _resolver;

        public GlossaryResolverServiceTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentGlossaryResolverTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            GlossaryService.InitializeForTests(_basePath);
            _resolver = new GlossaryResolverService();
        }

        [Fact]
        public void ResolveTerm_Should_Use_Scope_Precedence_Project_Then_Team_Then_User_Then_System()
        {
            GlossaryService.GetOrCreateProfile("ProjectA");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "agreement",
                    Target = "project-term",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Project,
                    ScopeOwnerId = "ProjectA",
                    LastAcceptedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                }
            }, isGlobal: false);

            GlossaryService.GetOrCreateProfile("TeamX");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "agreement",
                    Target = "team-term",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Team,
                    ScopeOwnerId = "TeamX",
                    LastAcceptedAt = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc)
                }
            }, isGlobal: false);

            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "agreement",
                    Target = "user-global-term",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.User,
                    ScopeOwnerId = "account-local",
                    LastAcceptedAt = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc)
                },
                new TermEntry
                {
                    Source = "agreement",
                    Target = "system-default-term",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.System,
                    ScopeOwnerId = "system-defaults",
                    LastAcceptedAt = new DateTime(2026, 1, 13, 0, 0, 0, DateTimeKind.Utc)
                }
            }, isGlobal: true);

            var result = _resolver.ResolveTerm("agreement", new TermResolutionContext
            {
                DomainVertical = DomainVertical.Legal,
                SourceLanguage = "English",
                TargetLanguage = "Turkish",
                ProjectId = "ProjectA",
                TeamId = "TeamX",
                UserId = "account-local"
            });

            result.Winner.Should().NotBeNull();
            result.Winner!.Target.Should().Be("project-term");
            result.ScopePrecedenceApplied.Should().Be("Project > Team > User > System");
            result.WinningRule.Should().Be("rule3_recency_after_scope");
            result.RequiresUserSelection.Should().BeFalse();
        }

        [Fact]
        public void ResolveTerm_Should_TieBreak_By_Most_Recent_LastAcceptedAt_Within_Same_Scope()
        {
            GlossaryService.GetOrCreateProfile("TeamA");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "indemnification",
                    Target = "tazminat-old",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Team,
                    ScopeOwnerId = "TeamA",
                    LastAcceptedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            }, isGlobal: false);

            GlossaryService.GetOrCreateProfile("TeamB");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "indemnification",
                    Target = "tazminat-new",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Team,
                    ScopeOwnerId = "TeamB",
                    LastAcceptedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            }, isGlobal: false);

            var result = _resolver.ResolveTerm("indemnification", new TermResolutionContext
            {
                DomainVertical = DomainVertical.Legal,
                SourceLanguage = "English",
                TargetLanguage = "Turkish"
            });

            result.Winner.Should().NotBeNull();
            result.Winner!.Target.Should().Be("tazminat-new");
            result.Reason.Should().Contain("LastAcceptedAt");
        }

        [Fact]
        public void ResolveTerm_Should_Respect_Domain_Exact_Match_Before_Scope()
        {
            GlossaryService.GetOrCreateProfile("LegalProject");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "claim",
                    Target = "hukuki-talep",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Project,
                    ScopeOwnerId = "LegalProject",
                    LastAcceptedAt = DateTime.UtcNow
                }
            }, isGlobal: false);

            GlossaryService.GetOrCreateProfile("PatentProject");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "claim",
                    Target = "patent-istem",
                    DomainVertical = DomainVertical.Patent,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Project,
                    ScopeOwnerId = "PatentProject",
                    LastAcceptedAt = DateTime.UtcNow
                }
            }, isGlobal: false);

            var result = _resolver.ResolveTerm("claim", new TermResolutionContext
            {
                DomainVertical = DomainVertical.Patent,
                SourceLanguage = "English",
                TargetLanguage = "Turkish",
                ProjectId = "PatentProject"
            });

            result.Winner.Should().NotBeNull();
            result.Winner!.Target.Should().Be("patent-istem");
            result.DecisionTrace.Should().Contain(x => x.Contains("Rule1"));
        }

        [Fact]
        public void ResolveTerm_Should_Surface_LowConfidence_Collision_For_UI_Selection()
        {
            DateTime tieTimestamp = new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc);

            GlossaryService.GetOrCreateProfile("TeamLeft");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "governing law",
                    Target = "uygulanacak-hukuk-a",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Team,
                    ScopeOwnerId = "TeamLeft",
                    LastAcceptedAt = tieTimestamp
                }
            }, isGlobal: false);

            GlossaryService.GetOrCreateProfile("TeamRight");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry
                {
                    Source = "governing law",
                    Target = "uygulanacak-hukuk-b",
                    DomainVertical = DomainVertical.Legal,
                    SourceLanguage = "English",
                    TargetLanguage = "Turkish",
                    ScopeType = GlossaryScopeType.Team,
                    ScopeOwnerId = "TeamRight",
                    LastAcceptedAt = tieTimestamp
                }
            }, isGlobal: false);

            var result = _resolver.ResolveTerm("governing law", new TermResolutionContext
            {
                DomainVertical = DomainVertical.Legal,
                SourceLanguage = "English",
                TargetLanguage = "Turkish"
            });

            result.Winner.Should().BeNull();
            result.IsLowConfidenceCollision.Should().BeTrue();
            result.RequiresUserSelection.Should().BeTrue();
            result.WinningRule.Should().Be("rule4_collision");
            result.Candidates.Should().HaveCount(2);
            result.DecisionTrace.Should().Contain(x => x.Contains("Rule4"));
        }

        public void Dispose()
        {
            GlossaryService.DisposeForTests();
            try
            {
                if (Directory.Exists(_basePath))
                {
                    Directory.Delete(_basePath, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
