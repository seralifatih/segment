using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class LearningConsentServiceTests : IDisposable
    {
        private readonly string _basePath;
        private readonly LearningConsentService _service;

        public LearningConsentServiceTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentLearningConsentTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            _service = new LearningConsentService();
        }

        private void ResetStore()
        {
            GlossaryService.InitializeForTests(_basePath);
            GlossaryService.GetOrCreateProfile("ProjectA");
        }

        [Fact]
        public void ApplyDecision_Should_Save_Global_When_Always_Selected()
        {
            ResetStore();
            var change = new DetectedChange { SourceTerm = "agreement", NewTerm = "sozlesme" };

            var outcome = _service.ApplyDecision(change, LearningConsentOption.Always);

            outcome.Saved.Should().BeTrue();
            outcome.IsGlobalScope.Should().BeTrue();
            GlossaryService.GlobalProfile.Terms.FindById("agreement")!.Target.Should().Be("sozlesme");
        }

        [Fact]
        public void ApplyDecision_Should_Save_Project_When_ThisProject_Selected()
        {
            ResetStore();
            var change = new DetectedChange { SourceTerm = "notice", NewTerm = "bildirim" };

            var outcome = _service.ApplyDecision(change, LearningConsentOption.ThisProject);

            outcome.Saved.Should().BeTrue();
            outcome.IsGlobalScope.Should().BeFalse();
            GlossaryService.CurrentProfile.Terms.FindById("notice")!.Target.Should().Be("bildirim");
        }

        [Fact]
        public void ApplyDecision_Should_Not_Change_Glossary_When_NotNow_Selected()
        {
            ResetStore();
            var change = new DetectedChange { SourceTerm = "waiver", NewTerm = "feragat" };

            var outcome = _service.ApplyDecision(change, LearningConsentOption.NotNow);

            outcome.Skipped.Should().BeTrue();
            GlossaryService.CurrentProfile.Terms.FindById("waiver").Should().BeNull();
        }

        [Fact]
        public void ApplyDecision_Should_Require_Explicit_Conflict_Resolution_Before_Overwrite()
        {
            ResetStore();
            GlossaryService.AddTerm("liability", "sorumluluk", isGlobal: false);
            var change = new DetectedChange { SourceTerm = "liability", NewTerm = "mesuliyet" };

            var outcome = _service.ApplyDecision(change, LearningConsentOption.ThisProject);

            outcome.RequiresConflictResolution.Should().BeTrue();
            GlossaryService.CurrentProfile.Terms.FindById("liability")!.Target.Should().Be("sorumluluk");
        }

        [Fact]
        public void ApplyDecision_Should_Preserve_Existing_When_User_Chooses_KeepExisting()
        {
            ResetStore();
            GlossaryService.AddTerm("damages", "tazminat", isGlobal: false);
            var change = new DetectedChange { SourceTerm = "damages", NewTerm = "zarar" };

            var outcome = _service.ApplyDecision(
                change,
                LearningConsentOption.ThisProject,
                _ => LearningConflictDecision.KeepExisting);

            outcome.Saved.Should().BeFalse();
            GlossaryService.CurrentProfile.Terms.FindById("damages")!.Target.Should().Be("tazminat");
        }

        [Fact]
        public void ApplyDecision_Should_Overwrite_When_User_Chooses_UseNewSuggestion()
        {
            ResetStore();
            GlossaryService.AddTerm("assign", "temlik et", isGlobal: false);
            var change = new DetectedChange { SourceTerm = "assign", NewTerm = "devret" };

            var outcome = _service.ApplyDecision(
                change,
                LearningConsentOption.ThisProject,
                _ => LearningConflictDecision.UseNewSuggestion);

            outcome.Saved.Should().BeTrue();
            outcome.ConflictResolvedWithOverwrite.Should().BeTrue();
            GlossaryService.CurrentProfile.Terms.FindById("assign")!.Target.Should().Be("devret");
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
            }
        }
    }
}
