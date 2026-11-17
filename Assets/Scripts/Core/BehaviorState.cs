namespace IgnisEscape.Core
{
    /// <summary>
    /// Tiga kelas perilaku resmi sesuai Bagian 3 spesifikasi (0 = Tenang, 1 = Ragu-Ragu, 2 = Panik).
    /// </summary>
    public enum BehaviorState
    {
        Tenang = 0,
        RaguRagu = 1,
        Panik = 2
    }

    public static class BehaviorStateExtensions
    {
        public static string ToProtocolString(this BehaviorState state)
        {
            switch (state)
            {
                case BehaviorState.Tenang:
                    return "Tenang";
                case BehaviorState.RaguRagu:
                    return "Ragu-Ragu";
                case BehaviorState.Panik:
                    return "Panik";
                default:
                    return "Tenang";
            }
        }

        public static bool TryParseProtocolString(string raw, out BehaviorState state)
        {
            switch (raw)
            {
                case "Tenang":
                    state = BehaviorState.Tenang;
                    return true;
                case "Ragu-Ragu":
                    state = BehaviorState.RaguRagu;
                    return true;
                case "Panik":
                    state = BehaviorState.Panik;
                    return true;
                default:
                    state = BehaviorState.Tenang;
                    return false;
            }
        }
    }
}

