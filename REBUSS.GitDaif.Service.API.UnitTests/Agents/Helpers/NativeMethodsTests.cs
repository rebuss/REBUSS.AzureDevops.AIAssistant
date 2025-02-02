using System;
using REBUSS.GitDaif.Service.API.Agents.Helpers;

namespace REBUSS.GitDaif.Service.API.Tests
{
    [TestFixture]
    public class NativeMethodsTests
    {
        [Test]
        public void GetClipboardText_NoTextInClipboard_ReturnsEmptyString()
        {
            // Arrange
            // Simulate no text in clipboard by ensuring clipboard is empty
            NativeMethods.ClearClipboard();
            
            // Act
            string result = NativeMethods.GetClipboardText();

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetClipboardText_TextInClipboard_ReturnsClipboardText()
        {
            // Arrange
            string expectedText = "Test clipboard text";
            NativeMethods.SetClipboardText(expectedText);

            // Act
            string result = NativeMethods.GetClipboardText();

            // Assert
            Assert.That(result, Is.EqualTo(expectedText));
        }
    }
}

