namespace ChildGame
{
    /// <summary>Numbers as children say them when counting out loud ("Một, hai, ba…" / "One, two, three…"), 1–99.</summary>
    public static class NumberWords
    {
        private static readonly string[] ViUnits = { "", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
        private static readonly string[] EnSmall =
        {
            "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
        };
        private static readonly string[] EnTens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

        public static string Say(int n, Language language)
        {
            if (n < 1 || n > 99) return n.ToString();
            var s = language == Language.Vietnamese ? Vietnamese(n) : English(n);
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        public static string Vietnamese(int n)
        {
            if (n < 10) return ViUnits[n];
            var tens = n / 10;
            var unit = n % 10;
            var head = tens == 1 ? "mười" : ViUnits[tens] + " mươi";
            if (unit == 0) return head;
            string tail;
            if (unit == 5) tail = "lăm";
            else if (unit == 1 && tens > 1) tail = "mốt";
            else if (unit == 4 && tens > 1) tail = "tư";
            else tail = ViUnits[unit];
            return head + " " + tail;
        }

        public static string English(int n)
        {
            if (n < 20) return EnSmall[n];
            var unit = n % 10;
            return EnTens[n / 10] + (unit > 0 ? "-" + EnSmall[unit] : "");
        }
    }
}
