using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentException"/> for target it is applied to when is not a valid credit card
    /// </summary>
    public sealed class CreditCardAttribute : ContractAspect
    {
        /// <summary>
        /// Validate value against contract implementation
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="name">Name</param>
        /// <param name="isParameter">Flag indicating if value is from a parameter</param>
        /// <param name="attrs">Attribute</param>
        /// <returns>Exception</returns>
        protected override Exception ValidateContract(object value, string name, bool isParameter, ContractAspect attr)
        {
            if (IsValid(value))
            {
                return null;
            }

            return new ArgumentException(name);
        }

        /// <summary>
        /// Return true if credit is valid using same code as .net
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns></returns>
        private bool IsValid(object value)
        {
            if (value == null)
            {
                return true;
            }

            string ccValue = value as string;
            if (ccValue == null)
            {
                return false;
            }
            ccValue = ccValue.Replace("-", "");
            ccValue = ccValue.Replace(" ", "");

            int checksum = 0;
            bool evenDigit = false;

            // http://www.beachnet.com/~hstiles/cardtype.html
            foreach (char digit in ccValue.Reverse())
            {
                if (digit < '0' || digit > '9')
                {
                    return false;
                }

                int digitValue = (digit - '0') * (evenDigit ? 2 : 1);
                evenDigit = !evenDigit;

                while (digitValue > 0)
                {
                    checksum += digitValue % 10;
                    digitValue /= 10;
                }
            }

            return (checksum % 10) == 0;
        }
    }
}
