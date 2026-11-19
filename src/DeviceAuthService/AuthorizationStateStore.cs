using System;
using Microsoft.Extensions.Caching.Memory;

namespace Ltwlf.Azure.B2C
{
    public interface IAuthorizationStateStore
    {
        void Set(AuthorizationState state, TimeSpan timeToLive);
        AuthorizationState GetByUserCode(string userCode);
        AuthorizationState GetByDeviceCode(string deviceCode);
    }

    // Pending device sign-ins only live for a few minutes, so they are kept in process memory.
    // This relies on the app running as a single, always-on instance (the shared wss-prd plan):
    // a restart or a second instance loses in-flight sign-ins, and the user requests a new code.
    public class MemoryAuthorizationStateStore : IAuthorizationStateStore
    {
        private readonly IMemoryCache _cache;

        public MemoryAuthorizationStateStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void Set(AuthorizationState state, TimeSpan timeToLive)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            _cache.Set(DeviceKey(state.DeviceCode), state, timeToLive);
            _cache.Set(UserKey(state.UserCode), state.DeviceCode, timeToLive);
        }

        public AuthorizationState GetByUserCode(string userCode)
        {
            if (string.IsNullOrEmpty(userCode)) return null;

            return _cache.TryGetValue(UserKey(userCode), out string deviceCode)
                ? GetByDeviceCode(deviceCode)
                : null;
        }

        public AuthorizationState GetByDeviceCode(string deviceCode)
        {
            if (string.IsNullOrEmpty(deviceCode)) return null;

            return _cache.TryGetValue(DeviceKey(deviceCode), out AuthorizationState state) ? state : null;
        }

        private static string DeviceKey(string deviceCode) => $"device:{deviceCode}";

        private static string UserKey(string userCode) => $"user:{userCode}";
    }
}
