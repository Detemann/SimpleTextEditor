using System;

namespace AED2
{
    public static class SpellChecker
    {
        public static int GetEditDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target == null ? 0 : target.Length;
            if (string.IsNullOrEmpty(target)) return source.Length;

            int[,] matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; matrix[i, 0] = i++) { }
            for (int j = 0; j <= target.Length; matrix[0, j] = j++) { }

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost
                    );
                }
            }
            return matrix[source.Length, target.Length];
        }

        public static int GetEditDistanceWithin(string source, string target, int maxDistance)
        {
            if (maxDistance < 0) return 1;
            if (string.IsNullOrEmpty(source)) return Math.Min(target == null ? 0 : target.Length, maxDistance + 1);
            if (string.IsNullOrEmpty(target)) return Math.Min(source.Length, maxDistance + 1);

            if (Math.Abs(source.Length - target.Length) > maxDistance) return maxDistance + 1;

            int[] previous = new int[target.Length + 1];
            int[] current = new int[target.Length + 1];

            for (int j = 0; j <= target.Length; j++) previous[j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                current[0] = i;
                int rowMin = current[0];

                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    current[j] = Math.Min(
                        Math.Min(previous[j] + 1, current[j - 1] + 1),
                        previous[j - 1] + cost
                    );

                    if (current[j] < rowMin) rowMin = current[j];
                }

                if (rowMin > maxDistance) return maxDistance + 1;

                int[] swap = previous;
                previous = current;
                current = swap;
            }

            int distance = previous[target.Length];
            return distance > maxDistance ? maxDistance + 1 : distance;
        }
    }
}
