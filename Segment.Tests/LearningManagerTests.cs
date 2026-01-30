using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class LearningManagerTests : IDisposable
    {
        private readonly MockNotificationService _mockNotification;

        public LearningManagerTests()
        {
            LearningManager.ResetForTesting();
            _mockNotification = new MockNotificationService();
            LearningManager.SetNotificationService(_mockNotification);
        }

        public void Dispose()
        {
            LearningManager.ResetForTesting();
        }

        [Fact]
        public void LearningManager_Should_Show_Toast_When_User_Changes_Term()
        {
            // Arrange
            string source = "Please submit the report";
            string aiOutput = "Lütfen raporu gönderin";
            string userOutput = "Lütfen raporu iletin"; // User changed "gönderin" to "iletin"

            // Act
            LearningManager.ProcessUserEdit(source, aiOutput, userOutput);

            // Assert
            _mockNotification.CallCount.Should().Be(1, "learning should detect the term change");
            var change = _mockNotification.ToastCalls[0];
            change.Should().NotBeNull();
            change.OldTerm.Should().Be("gönderin", "the AI used 'gönderin'");
            change.NewTerm.Should().Be("iletin", "the user changed it to 'iletin'");
        }

        [Fact]
        public void LearningManager_Should_Not_Show_Toast_When_Text_Is_Identical()
        {
            // Arrange
            string source = "Please submit the report";
            string aiOutput = "Lütfen raporu gönderin";
            string userOutput = "Lütfen raporu gönderin"; // Exactly the same

            // Act
            LearningManager.ProcessUserEdit(source, aiOutput, userOutput);

            // Assert
            _mockNotification.CallCount.Should().Be(0, "no change detected, so no toast should appear");
        }

        [Fact]
        public void LearningManager_Should_Not_Show_Toast_When_Similarity_Is_Too_Low()
        {
            // Arrange
            string source = "Please submit the report";
            string aiOutput = "Lütfen raporu gönderin";
            string userOutput = "Tamamen farklı bir metin"; // Completely different (similarity < 0.5)

            // Act
            LearningManager.ProcessUserEdit(source, aiOutput, userOutput);

            // Assert
            _mockNotification.CallCount.Should().Be(0, "text too different, should be ignored");
        }

        [Fact]
        public void LearningManager_Should_Not_Show_Toast_When_Change_Is_Too_Large()
        {
            // Arrange
            string source = "Please submit the report immediately";
            string aiOutput = "Lütfen raporu hemen gönderin";
            string userOutput = "Lütfen raporu şimdi derhal acilen iletin anında"; // Changed too many words (>4)

            // Act
            LearningManager.ProcessUserEdit(source, aiOutput, userOutput);

            // Assert
            _mockNotification.CallCount.Should().Be(0, "change is too large to be a terminology fix");
        }

        [Fact]
        public void LearningManager_Should_Pass_Correct_Data_To_Toast()
        {
            // Arrange
            string source = "sign the agreement";
            string aiOutput = "sözleşmeyi imzala";
            string userOutput = "sözleşmeyi mühürle"; // Changed "imzala" to "mühürle"

            // Act
            LearningManager.ProcessUserEdit(source, aiOutput, userOutput);

            // Assert
            _mockNotification.CallCount.Should().Be(1);
            var change = _mockNotification.ToastCalls[0];
            change.FullSourceText.Should().Be(source, "source text should be preserved");
            change.OldTerm.Should().Be("imzala");
            change.NewTerm.Should().Be("mühürle");
        }

        [Fact]
        public void TermDetective_Should_Return_Null_For_Identical_Strings()
        {
            // Arrange
            string source = "test";
            string ai = "çeviri";
            string user = "çeviri";

            // Act
            var result = TermDetective.Analyze(source, ai, user);

            // Assert
            result.Should().BeNull("no change detected");
        }

        [Fact]
        public void TermDetective_Should_Detect_Single_Word_Change()
        {
            // Arrange
            string source = "send";
            string ai = "gönder";
            string user = "ilet";

            // Act
            var result = TermDetective.Analyze(source, ai, user);

            // Assert
            result.Should().NotBeNull();
            result!.OldTerm.Should().Be("gönder");
            result.NewTerm.Should().Be("ilet");
        }

        [Fact]
        public void TermDetective_Should_Detect_Change_In_Middle_Of_Sentence()
        {
            // Arrange
            string source = "Please sign the document";
            string ai = "Lütfen belgeyi imzalayın";
            string user = "Lütfen belgeyi mühürleyin";

            // Act
            var result = TermDetective.Analyze(source, ai, user);

            // Assert
            result.Should().NotBeNull();
            result!.OldTerm.Should().Be("imzalayın");
            result.NewTerm.Should().Be("mühürleyin");
        }
    }
}
