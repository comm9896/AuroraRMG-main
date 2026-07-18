using System;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Minimal linear-algebra helpers for the import-time layout algorithms
    /// (spectral embedding and classical MDS). All routines are deterministic
    /// and operate on small, dense, symmetric matrices, so a straightforward
    /// Jacobi eigen-decomposition is used — no external dependencies required.
    /// </summary>
    internal static class LinearAlgebra
    {
        /// <summary>
        /// Computes all eigenvalues/eigenvectors of a real SYMMETRIC matrix <paramref name="a"/>
        /// (size n×n) via the cyclic Jacobi rotation method. Returns the eigenvalues (ascending)
        /// and, in <paramref name="vectors"/>, the corresponding (column) eigenvectors.
        /// The input matrix is destroyed in the process.
        /// </summary>
        public static double[] JacobiEigen(double[][] a, out double[][] vectors)
        {
            int n = a.Length;
            vectors = new double[n][];
            for (int i = 0; i < n; i++)
            {
                vectors[i] = new double[n];
                vectors[i][i] = 1.0;
            }

            const int maxSweeps = 100;
            const double eps = 1e-12;
            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                double off = 0.0;
                for (int p = 0; p < n; p++)
                    for (int q = p + 1; q < n; q++)
                        off += a[p][q] * a[p][q];
                if (off <= eps) break;

                for (int p = 0; p < n; p++)
                {
                    for (int q = p + 1; q < n; q++)
                    {
                        double apq = a[p][q];
                        if (Math.Abs(apq) < eps) continue;

                        double app = a[p][p];
                        double aqq = a[q][q];
                        double phi = 0.5 * Math.Atan2(2.0 * apq, aqq - app);
                        double c = Math.Cos(phi);
                        double s = Math.Sin(phi);

                        // Rotate rows/cols p and q of A and the eigenvector matrix.
                        for (int k = 0; k < n; k++)
                        {
                            double akp = a[k][p];
                            double akq = a[k][q];
                            a[k][p] = c * akp - s * akq;
                            a[k][q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double apk = a[p][k];
                            double aqk = a[q][k];
                            a[p][k] = c * apk - s * aqk;
                            a[q][k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double vkp = vectors[k][p];
                            double vkq = vectors[k][q];
                            vectors[k][p] = c * vkp - s * vkq;
                            vectors[k][q] = s * vkp + c * vkq;
                        }
                    }
                }
            }

            var values = new double[n];
            for (int i = 0; i < n; i++)
                values[i] = a[i][i];

            // Sort ascending, dragging eigenvectors along.
            // Stored column-major: sortedVecs[eigenRank][node] = eigenvector #order[eigenRank]
            // evaluated at node index `node`. This lets callers read a node's coordinate
            // on a given eigen-axis as sortedVecs[rank][node].
            var order = Enumerable.Range(0, n).OrderBy(i => values[i]).ToArray();
            var sortedValues = new double[n];
            var sortedVecs = new double[n][];
            for (int r = 0; r < n; r++)
            {
                sortedValues[r] = values[order[r]];
                int srcCol = order[r];
                sortedVecs[r] = new double[n];
                for (int node = 0; node < n; node++)
                    sortedVecs[r][node] = vectors[node][srcCol];
            }
            vectors = sortedVecs;
            return sortedValues;
        }

        /// <summary>
        /// All-pairs shortest-path on an undirected weighted graph (Floyd–Warshall).
        /// <paramref name="adj"/>[i] is the neighbour list (parallel to <paramref name="weight"/>),
        /// <paramref name="weight"/>[i][j] is the edge weight between i and adj[i][j].
        /// Returns an n×n matrix; unreachable pairs are <see cref="double.PositiveInfinity"/>.
        /// </summary>
        public static double[][] AllPairsShortest(int n,
            System.Collections.Generic.List<int>[] adj,
            System.Collections.Generic.List<double>[] weight)
        {
            var dist = new double[n][];
            for (int i = 0; i < n; i++)
            {
                dist[i] = new double[n];
                for (int j = 0; j < n; j++)
                    dist[i][j] = (i == j) ? 0.0 : double.PositiveInfinity;
            }
            for (int i = 0; i < n; i++)
                for (int e = 0; e < adj[i].Count; e++)
                {
                    int j = adj[i][e];
                    if (j > i) // undirected: set once
                        dist[i][j] = dist[j][i] = Math.Min(dist[i][j], weight[i][e]);
                }
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        double via = dist[i][k] + dist[k][j];
                        if (via < dist[i][j]) dist[i][j] = via;
                    }
            return dist;
        }

        /// <summary>Matrix × vector.</summary>
        public static double[] MatVec(double[][] m, double[] v)
        {
            int n = m.Length;
            var r = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0.0;
                for (int j = 0; j < n; j++) s += m[i][j] * v[j];
                r[i] = s;
            }
            return r;
        }
    }
}
