using System.Collections.Concurrent;

namespace FirebirdWeb.Helpers
{
    public static class TempOTPStore
    {
        private static readonly ConcurrentDictionary<string, string> Store = new();

        public static void StoreOTP(string email, string otp)
        {
            Store[email] = otp;
        }

        public static bool ValidateOTP(string email, string otp)
        {
            return Store.TryGetValue(email, out var storedOtp) && storedOtp == otp;
        }

        public static void RemoveOTP(string email)
        {
            Store.TryRemove(email, out _);
        }
    }
}