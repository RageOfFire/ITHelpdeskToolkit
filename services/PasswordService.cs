using System;
using System.Security.Cryptography;
using System.Text;

namespace ITHelpdeskToolkit.Services
{
    public static class PasswordService
    {
        private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
        private const string NumericChars = "0123456789";
        private const string SpecialChars = "!@#$%^&*()-_=+";

        public static (string Password, string Status) GeneratePassword(
            int length,
            bool useUpper,
            bool useLower,
            bool useNumbers,
            bool useSpecial)
        {
            if (length < 8 || length > 128)
            {
                return ("", "Length must be between 8 and 128.");
            }

            StringBuilder charPool = new();
            if (useUpper) charPool.Append(UppercaseChars);
            if (useLower) charPool.Append(LowercaseChars);
            if (useNumbers) charPool.Append(NumericChars);
            if (useSpecial) charPool.Append(SpecialChars);

            string pool = charPool.ToString();
            if (string.IsNullOrEmpty(pool))
            {
                return ("", "Select at least one option.");
            }

            StringBuilder result = new(length);
            byte[] randomNumber = new byte[4];

            for (int i = 0; i < length; i++)
            {
                RandomNumberGenerator.Fill(randomNumber);
                uint val = BitConverter.ToUInt32(randomNumber, 0);
                int idx = (int)(val % (uint)pool.Length);
                result.Append(pool[idx]);
            }

            return (result.ToString(), "Password generated.");
        }
    }
}
