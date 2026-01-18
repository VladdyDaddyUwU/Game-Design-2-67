using UnityEngine;
using System.Collections.Generic;

public static class StructuralAnalysis
{
    // A simple Matrix class for FEM calculations
    public class Matrix
    {
        private readonly double[,] data;
        public int Rows { get; }
        public int Cols { get; }

        public Matrix(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            data = new double[rows, cols];
        }

        public double this[int row, int col]
        {
            get { return data[row, col]; }
            set { data[row, col] = value; }
        }

        public static int CalculateRank(Matrix A)
        {
            int m = A.Rows;
            int n = A.Cols;
            double[,] mat = (double[,])A.data.Clone();
            int rank = 0;

            for (int j = 0; j < n && rank < m; j++)
            {
                int pivot = rank;
                for (int i = rank + 1; i < m; i++)
                {
                    if (System.Math.Abs(mat[i, j]) > System.Math.Abs(mat[pivot, j]))
                        pivot = i;
                }

                if (System.Math.Abs(mat[pivot, j]) > 1.0)
                {
                    // Swap rows
                    for (int k = j; k < n; k++)
                    {
                        double temp = mat[rank, k];
                        mat[rank, k] = mat[pivot, k];
                        mat[pivot, k] = temp;
                    }

                    // Eliminate rows below
                    for (int i = rank + 1; i < m; i++)
                    {
                        double factor = mat[i, j] / mat[rank, j];
                        for (int k = j; k < n; k++)
                            mat[i, k] -= factor * mat[rank, k];
                    }
                    rank++;
                }
            }
            return rank;
        }

        public string ToFormattedString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Matrix Data ({Rows}x{Cols}):");
            for (int i = 0; i < Rows; i++)
            {
                string row = "";
                for (int j = 0; j < Cols; j++)
                {
                    // Using scientific notation with 2 decimals for readability
                    row += data[i, j].ToString("E2").PadLeft(11) + " ";
                }
                sb.AppendLine(row);
            }
            return sb.ToString();
        }

        // Basic matrix solver using Gaussian elimination for Ax = b
        public static double[] Solve(Matrix A, double[] b)
        {
            int n = A.Rows;
            Matrix Ab = new Matrix(n, n + 1);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    Ab[i, j] = A[i, j];
                }
                Ab[i, n] = b[i];
            }

            // Forward elimination
            for (int i = 0; i < n; i++)
            {
                // Find pivot
                int maxRow = i;
                for (int k = i + 1; k < n; k++)
                {
                    if (System.Math.Abs(Ab[k, i]) > System.Math.Abs(Ab[maxRow, i]))
                    {
                        maxRow = k;
                    }
                }

                // Swap rows
                for (int k = i; k < n + 1; k++)
                {
                    double temp = Ab[i, k];
                    Ab[i, k] = Ab[maxRow, k];
                    Ab[maxRow, k] = temp;
                }
                
                // Debug Pivot
                // Debug.Log($"[Matrix Debug] Row {i}: Pivot Value = {Ab[i, i]:E4}");

                // Check for singular or near-singular matrix (Mechanism detected)
                // Threshold increased to 1.0 to catch "floppy" mechanisms caused by tiny coordinate noise.
                // Relative to 10^8 stiffness, 1.0 is effectively zero.
                if (System.Math.Abs(Ab[i, i]) <= 1.0)
                {
                    Debug.LogError($"[Matrix Solver FAILED] Matrix is singular or has extremely weak stiffness at row {i} (Pivot: {Ab[i, i]:E4}). The structure is likely a mechanism.");
                    return null;
                }

                // Make all rows below this one 0 in this column
                for (int k = i + 1; k < n; k++)
                {
                    double factor = Ab[k, i] / Ab[i, i];
                    for (int j = i; j < n + 1; j++)
                    {
                        Ab[k, j] -= factor * Ab[i, j];
                    }
                }
            }

            // Back substitution
            double[] solution = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = 0.0;
                for (int j = i + 1; j < n; j++)
                {
                    sum += Ab[i, j] * solution[j];
                }
                solution[i] = (Ab[i, n] - sum) / Ab[i, i];
            }

            return solution;
        }
    }

    public struct AnalysisResult
    {
        public bool IsStable;
        public Vector2[] Displacements;
        public float[] MemberForces;
        public float[] MemberStressPercentages;
        public Vector2[] ReactionForces; // Reaction forces at anchors
    }

    public static AnalysisResult RunAnalysis(
        List<Vector2> nodePositions,
        List<int[]> elements,
        List<int> fixedNodeIndices,
        Dictionary<int, Vector2> externalLoads,
        float youngsModulus,
        float[] elementAreas, // CHANGED: Now an array matching elements count
        float yieldStress,
        float density = 7850f) // Default to Steel density (kg/m3)
    {
        int numNodes = nodePositions.Count;
        int numElems = elements.Count;
        int totalDOFs = 2 * numNodes;

        // --- Stiffness Matrix ---
        var K = new Matrix(totalDOFs, totalDOFs);
        var elementLengths = new double[numElems];
        var F = new double[totalDOFs]; // Global Force Vector

        // Apply Gravity (Self-Weight) and Build Stiffness Matrix
        for (int e = 0; e < numElems; e++)
        {
            int ni = elements[e][0];
            int nj = elements[e][1];
            float area = elementAreas[e]; // Get specific area for this beam

            Vector2 pos_i = nodePositions[ni];
            Vector2 pos_j = nodePositions[nj];

            double dx = pos_j.x - pos_i.x;
            double dy = pos_j.y - pos_i.y;
            double L = Mathf.Sqrt((float)(dx * dx + dy * dy));
            elementLengths[e] = L;

            // 1. Calculate Self-Weight for this beam
            // Volume = Area * Length. Mass = Density * Volume.
            // Force = Mass * Gravity (-9.81).
            double beamMass = density * area * L;
            double beamWeight = beamMass * -9.81;
            
            // Distribute half the weight to each node (Lumped Mass approach)
            F[2 * ni + 1] += beamWeight / 2.0;
            F[2 * nj + 1] += beamWeight / 2.0;

            // 2. Stiffness Matrix Calculation
            double cx = dx / L;
            double cy = dy / L;
            double stiffness = area * youngsModulus / L;

            var ke = new double[4, 4]
            {
                { cx*cx, cx*cy, -cx*cx, -cx*cy },
                { cx*cy, cy*cy, -cx*cy, -cy*cy },
                { -cx*cx, -cx*cy, cx*cx, cx*cy },
                { -cx*cy, -cy*cy, cx*cy, cy*cy }
            };

            int[] dof = { 2*ni, 2*ni+1, 2*nj, 2*nj+1 };

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    K[dof[i], dof[j]] += stiffness * ke[i, j];
                }
            }
        }

        // --- Apply External Loads (Anvil, etc) ---
        foreach(var load in externalLoads)
        {
            F[2 * load.Key] += load.Value.x;
            F[2 * load.Key + 1] += load.Value.y;
        }

        // --- Partition Matrix for Solver ---
        List<int> fixedDOFs = new List<int>();
        foreach(int nodeIndex in fixedNodeIndices)
        {
            fixedDOFs.Add(2 * nodeIndex);
            fixedDOFs.Add(2 * nodeIndex + 1);
        }

        List<int> freeDOFs = new List<int>();
        for(int i=0; i < totalDOFs; i++)
        {
            if(!fixedDOFs.Contains(i))
                freeDOFs.Add(i);
        }

        if (freeDOFs.Count == 0) return new AnalysisResult { IsStable = true }; // No free nodes

        // K_ff * u_f = F_f
        Matrix K_ff = new Matrix(freeDOFs.Count, freeDOFs.Count);
        double[] F_f = new double[freeDOFs.Count];

        for(int i=0; i<freeDOFs.Count; i++)
        {
            F_f[i] = F[freeDOFs[i]];
            for(int j=0; j<freeDOFs.Count; j++)
            {
                K_ff[i,j] = K[freeDOFs[i], freeDOFs[j]];
            }
        }

        // --- DEBUG: Rank & Nullity Check ---
        int rank = Matrix.CalculateRank(K_ff);
        int numVars = freeDOFs.Count;
        int nullity = numVars - rank;
        
        Debug.Log($"[Analysis Debug] Free DOFs: {numVars}, Rank(K_ff): {rank}, Nullity: {nullity}");
        Debug.Log($"[Matrix Dump] Stiffness Matrix (K_ff):\n{K_ff.ToFormattedString()}");
        
        if (nullity > 0)
        {
            Debug.LogError($"[Analysis FAILED] Mechanism detected! Nullity is {nullity} (should be 0). The structure has {nullity} unconstrained degrees of freedom.");
            return new AnalysisResult { IsStable = false };
        }

        double[] u_f = Matrix.Solve(K_ff, F_f);
        
        if (u_f == null) // Solver failed (Unstable)
        {
            Debug.Log($"[Matrix Dump] Full Stiffness Matrix (K_ff) that caused the solver failure:\n{K_ff.ToFormattedString()}");
            return new AnalysisResult { IsStable = false };
        }

        // Sanity Check: Mechanism Detection via Large Displacements
        // If the structure is a mechanism, displacements will blow up to infinity (or very large numbers).
        foreach (double val in u_f)
        {
            if (System.Math.Abs(val) > 10.0) 
            {
                Debug.LogWarning($"Simulation detected extremely large displacements (> 10m). Treating as unstable mechanism. Value: {val}");
                return new AnalysisResult { IsStable = false };
            }
        }

        // Reconstruct full displacement vector u
        var u = new double[totalDOFs];
        for(int i=0; i<freeDOFs.Count; i++)
        {
            u[freeDOFs[i]] = u_f[i];
        }

        // --- Post-processing ---
        var memberForces = new float[numElems];
        var stressPercentages = new float[numElems];
        var reactionForces = new Vector2[numNodes]; // Only relevant for anchors

        for (int e = 0; e < numElems; e++)
        {
            int ni = elements[e][0];
            int nj = elements[e][1];
            float area = elementAreas[e]; // Use specific area

            double L = elementLengths[e];
            double dx = nodePositions[nj].x - nodePositions[ni].x;
            double dy = nodePositions[nj].y - nodePositions[ni].y;
            double cx = dx / L;
            double cy = dy / L;

            var ue = new double[] { u[2*ni], u[2*ni+1], u[2*nj], u[2*nj+1] };
            
            // Axial Force: F = (EA/L) * ChangeInLength
            double force = (area * youngsModulus / L) * (-cx * ue[0] - cy * ue[1] + cx * ue[2] + cy * ue[3]);
            memberForces[e] = (float)force;
            
            // Calculate Stress Percentage
            float maxTensileForce = (float)(area * yieldStress);
            
            // Euler Buckling Critical Load (P_cr = pi^2 * E * I / L^2)
            // Assuming square cross section: I = a^4 / 12 = A^2 / 12
            float momentOfInertia = (float) (Mathf.Pow(Mathf.Sqrt(area), 4) / 12.0f);
            float maxCompressiveBucklingLoad = (float)((Mathf.PI * Mathf.PI * youngsModulus * momentOfInertia) / ((float)L * (float)L));

            if (Mathf.Abs((float)force) < 1e-4)
            {
                stressPercentages[e] = 0;
            }
            else if (force > 0) // Tension
            {
                stressPercentages[e] = (float)(force / maxTensileForce) * 100f;
            }
            else // Compression
            {
                // MATLAB Logic: percentages(i) = max(-memberForces(i)/P(i) * 100, -memberForces(i)/Fmax * 100);
                float bucklingPercentage = (float)(-force / maxCompressiveBucklingLoad) * 100f;
                float yieldPercentage = (float)(-force / maxTensileForce) * 100f;
                
                stressPercentages[e] = Mathf.Max(bucklingPercentage, yieldPercentage);
            }
        }

        // --- Reaction Forces (Optional check) ---
        // R = K * u - F_external
        // For fixed nodes, u is 0, so R_fixed = K_fixed_free * u_free - F_fixed
        // This calculates how hard the ground is pulling back.

        var displacements = new Vector2[numNodes];
        for (int i = 0; i < numNodes; i++)
        {
            displacements[i] = new Vector2((float)u[2*i], (float)u[2*i+1]);
        }
        
        // We have successfully solved the system. 
        // Whether the materials fail (stress > 100%) is a separate check for the Game Manager.
        bool isStable = true; 

        return new AnalysisResult
        {
            IsStable = isStable,
            Displacements = displacements,
            MemberForces = memberForces,
            MemberStressPercentages = stressPercentages,
            ReactionForces = reactionForces
        };
    }
}