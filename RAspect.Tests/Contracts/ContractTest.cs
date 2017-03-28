using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RAspect.Tests.Contracts
{
    /// <summary>
    /// Contract Test
    /// </summary>
    public class ContractTest
    {
        public ContractModel model;

        public ContractTest()
        {
            model = new ContractModel();
        }

        [Fact]
        public void WillThrowForInvalidCreditCard()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateCreditCard("4373-1000-8888-7888"));
        }

        [Fact]
        public void CreditCardShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateCreditCard("4373-1345-1245-1366");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForInvalidEmail()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidEmailAddress("test@test"));
        }

        [Fact]
        public void EmailShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidEmailAddress("test@test.com");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForInvalidEnum()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidEnumData("V100"));
        }

        [Fact]
        public void EnumShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidEnumData("V2");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForGreaterThan10()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateGreaterThan10(9));
        }

        [Fact]
        public void GreatherThan10ShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateGreaterThan10(11);
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForValueGreaterThanLesser10()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateLessThan10(10));
        }

        [Fact]
        public void LessThan10ShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateLessThan10(9);
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForNegative()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateNegative(1));
        }

        [Fact]
        public void NonNegativeShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateNegative(-1);
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForNotEmpty()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateNotEmpty(""));
        }

        [Fact]
        public void NotEmptyShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateNotEmpty("Value");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForNotNull()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateNotNull(null));
        }

        [Fact]
        public void NotNullShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateNotNull("Value");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForBadPhone()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidatePhone("999-abc-1000"));
        }

        [Fact]
        public void PhoneShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidatePhone("555-555-5555");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForNonPositiveNumber()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidatePositive(-1));
        }

        [Fact]
        public void PositiveShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidatePositive(1);
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForNotRange_0_10()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateRange0_10(11));
        }

        [Fact]
        public void Range_0_10_ShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateRange0_10(1);
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForInvalidNumber()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateNumberRegex("abc"));
        }

        [Fact]
        public void NumberValueShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateNumberRegex("1");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForNullNotRequired()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateRequired(null));
        }

        [Fact]
        public void ValueIsRequiredShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateRequired("value");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForStringGreaterThan10Length()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateStringLength10("123456789010"));
        }

        [Fact]
        public void StringIsInRangeOfLengthShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateStringLength10("123456789A");
            }
            catch { }

            Assert.Null(ex);
        }

        [Fact]
        public void WillThrowForInvalidUrl()
        {
            Assert.ThrowsAny<Exception>(() =>
                model.ValidateUrl(""));
        }

        [Fact]
        public void UrlShouldBeValid()
        {
            Exception ex = null;
            try
            {
                model.ValidateUrl("http://www.yahoo.com");
            }
            catch { }

            Assert.Null(ex);
        }
    }
}
