namespace Core.Shared.Extensions;

public static class EnumerableExtension
{
	public static double WeightedAverage<T>(this IEnumerable<T> records, Func<T, double> value, Func<T, double> weight)
	{
		if (records is null)
			throw new ArgumentNullException(nameof(records), $"{nameof(records)} is null.");

		int count = 0;
		double valueSum = 0;
		double weightSum = 0;

		foreach (T? record in (List<T>)records)
		{
			count++;
			double recordWeight = weight(record);

			valueSum += value(record) * recordWeight;
			weightSum += recordWeight;
		}

		return (count == 0)
			? throw new ArgumentException($"{nameof(records)} is empty.")
			: (count == 1)
			? value(records.Single())
			: (weightSum != 0)
			? valueSum / weightSum
			: throw new DivideByZeroException($"Division of {valueSum.ToString()} by zero.");
	}

	public static double StandardDeviation(this IEnumerable<double> values)
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values), $"{nameof(values)} is null.");

		values = [.. values];
		double avg = values.Average();
		return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
	}
}