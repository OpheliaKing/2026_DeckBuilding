using System;
using System.Globalization;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 총 데미지를 가중치(배율)에 따라 정수로 나눕니다. 합계는 항상 totalDamage와 같습니다.
    /// 예: total=40, weights=[1,1,2] → [10,10,20]
    /// </summary>
    public static class CombatDamageSplit
    {
        public static int[] SplitByWeights(int totalDamage, float[] weights)
        {
            if (totalDamage <= 0)
                return weights == null || weights.Length == 0 ? Array.Empty<int>() : new int[weights.Length];

            if (weights == null || weights.Length == 0)
                return new[] { totalDamage };

            int count = weights.Length;
            var result = new int[count];

            double weightSum = 0d;
            for (int i = 0; i < count; i++)
                weightSum += Math.Max(0d, weights[i]);

            if (weightSum <= 0d)
            {
                // 전부 0이면 마지막 히트에 몰아줌
                result[count - 1] = totalDamage;
                return result;
            }

            var fractions = new double[count];
            int assigned = 0;
            for (int i = 0; i < count; i++)
            {
                double w = Math.Max(0d, weights[i]);
                double exact = totalDamage * (w / weightSum);
                result[i] = (int)Math.Floor(exact);
                fractions[i] = exact - result[i];
                assigned += result[i];
            }

            int remain = totalDamage - assigned;
            // 소수부가 큰 히트부터 1씩 배분 (동점이면 뒤쪽 우선)
            while (remain > 0)
            {
                int best = -1;
                double bestFrac = -1d;
                for (int i = 0; i < count; i++)
                {
                    if (fractions[i] > bestFrac + 1e-9 ||
                        (Math.Abs(fractions[i] - bestFrac) <= 1e-9 && i > best))
                    {
                        bestFrac = fractions[i];
                        best = i;
                    }
                }

                if (best < 0)
                    best = count - 1;

                result[best]++;
                fractions[best] = -1d; // 한 번 받은 곳은 다음 라운드에서 뒤로
                remain--;
            }

            return result;
        }

        public static float[] ParseWeightsCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return new[] { 1f };

            var parts = csv.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return new[] { 1f };

            var weights = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out weights[i]))
                    weights[i] = 1f;
                if (weights[i] < 0f)
                    weights[i] = 0f;
            }

            return weights;
        }
    }
}
