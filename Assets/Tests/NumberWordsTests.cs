using NUnit.Framework;

namespace ChildGame.Tests
{
    public class NumberWordsTests
    {
        [TestCase(1, "Một")]
        [TestCase(5, "Năm")]
        [TestCase(10, "Mười")]
        [TestCase(11, "Mười một")]
        [TestCase(15, "Mười lăm")]
        [TestCase(20, "Hai mươi")]
        [TestCase(21, "Hai mươi mốt")]
        [TestCase(24, "Hai mươi tư")]
        [TestCase(35, "Ba mươi lăm")]
        public void Vietnamese(int n, string said) => Assert.AreEqual(said, NumberWords.Say(n, Language.Vietnamese));

        [TestCase(3, "Three")]
        [TestCase(13, "Thirteen")]
        [TestCase(40, "Forty")]
        [TestCase(42, "Forty-two")]
        public void English(int n, string said) => Assert.AreEqual(said, NumberWords.Say(n, Language.English));
    }
}
