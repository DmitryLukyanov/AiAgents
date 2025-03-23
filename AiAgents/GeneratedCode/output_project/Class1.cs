using System;

namespace FactorialCalculator
{
    public class Calculator
    {
        // Method to calculate the factorial of a number
        public long CalculateFactorial(int number)
        {
            if (number < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Number must be non-negative.");
            }

            long factorial = 1;
            for (int i = 1; i <= number; i++)
            {
                factorial *= i;
            }
            return factorial;
        }
    }
}