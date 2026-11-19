using System;
using System.Threading;
using Ltwlf.Azure.B2C;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeviceAuthService.Tests
{
    public class MemoryAuthorizationStateStoreTest
    {
        private static MemoryAuthorizationStateStore CreateStore() =>
            new MemoryAuthorizationStateStore(new MemoryCache(new MemoryCacheOptions()));

        private static AuthorizationState CreateState() =>
            new AuthorizationState { DeviceCode = "device-123", UserCode = "ABC123", ExpiresIn = 360 };

        [Fact]
        public void Set_ThenLookUpByEitherCode_ReturnsTheState()
        {
            var store = CreateStore();
            var state = CreateState();

            store.Set(state, TimeSpan.FromMinutes(5));

            Assert.Same(state, store.GetByUserCode("ABC123"));
            Assert.Same(state, store.GetByDeviceCode("device-123"));
        }

        [Fact]
        public void UnknownOrEmptyCodes_ReturnNull()
        {
            var store = CreateStore();
            store.Set(CreateState(), TimeSpan.FromMinutes(5));

            Assert.Null(store.GetByUserCode("ZZZ999"));
            Assert.Null(store.GetByDeviceCode("other"));
            Assert.Null(store.GetByUserCode(null));
            Assert.Null(store.GetByDeviceCode(""));
        }

        [Fact]
        public void Set_AgainWithTokens_ReplacesTheState()
        {
            var store = CreateStore();
            store.Set(CreateState(), TimeSpan.FromMinutes(5));

            var signedIn = CreateState();
            signedIn.AccessToken = "token";
            store.Set(signedIn, TimeSpan.FromMinutes(5));

            Assert.Equal("token", store.GetByDeviceCode("device-123").AccessToken);
            Assert.Equal("token", store.GetByUserCode("ABC123").AccessToken);
        }

        [Fact]
        public void Entries_ExpireAfterTimeToLive()
        {
            var store = CreateStore();
            store.Set(CreateState(), TimeSpan.FromMilliseconds(50));

            Thread.Sleep(150);

            Assert.Null(store.GetByDeviceCode("device-123"));
            Assert.Null(store.GetByUserCode("ABC123"));
        }
    }
}
