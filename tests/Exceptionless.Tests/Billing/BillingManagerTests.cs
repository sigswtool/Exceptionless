using System;
using Exceptionless.Core.Billing;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Billing {
    public class BillingManagerTests : TestWithServices {
        public BillingManagerTests(ServicesFixture fixture) : base(fixture) {}

        [Fact]
        public void GetBillingPlan() {
            var billingManager = GetService<BillingManager>();
            Assert.Equal(billingManager.FreePlan.Id, billingManager.GetBillingPlan(BillingManager.FreePlan.Id).Id);
        }
    }
}