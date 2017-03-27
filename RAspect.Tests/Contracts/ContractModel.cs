using RAspect.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Tests.Contracts
{
    public enum MyEnum
    {
        V1 = 1,
        V2 = 2,
        V3 = 3
    }
    public class ContractModel
    {
        public void ValidateCreditCard([CreditCard]string cardNumber) { }
        public void ValidEmailAddress([EmailAddress]string email) { }
        public void ValidEnumData([EnumDataType(typeof(MyEnum))]string value) { }
        public void ValidateGreaterThan10([GreaterThan(10)]int value) { }
        public void ValidateLessThan10([LessThan(10)]int value) { }
        public void ValidateNegative([Negative]int value) { }
        public void ValidateNotEmpty([NotEmpty]string value) { }
        public void ValidateNotNull([NotNull]string value) { }
        public void ValidatePhone([Phone]string phone) { }
        public void ValidatePositive([Positive]int value) { }
        public void ValidateRange0_10([Range(0, 10)]int value) { }
        public void ValidateNumberRegex([RegularExpression(@"(\d+)")]string value) { }
        public void ValidateRequired([Required]string value) { }
        public void ValidateStringLength10([StringLength(10)]string value) { }
        public void ValidateUrl([Url]string url) { }
    }
}
