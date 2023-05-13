namespace XenophyteAndroidWallet.Extension
{
    public static class StringExtension
    {
        public static string SafeSubstring(this string text, int start, int length)
        {
            if (text == null) return null;

            return text.Length <= start ? ""
            : text.Length - start <= length ? text.Substring(start)
            : text.Substring(start, length);
        }
    }
}