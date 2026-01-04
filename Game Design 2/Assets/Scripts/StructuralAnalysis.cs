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
                    if (Mathf.Abs((float)Ab[k, i]) > Mathf.Abs((float)Ab[maxRow, i]))
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
                
                // Check for singular or near-singular matrix (Mechanism detected)
                if (Mathf.Abs((float)Ab[i,i]) <= 1e-9)
                {
                    Debug.LogError($"Matrix is singular at row {i}. The structure is a mechanism (unstable).");
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
        float crossSectionArea,
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

            Vector2 pos_i = nodePositions[ni];
            Vector2 pos_j = nodePositions[nj];

            double dx = pos_j.x - pos_i.x;
            double dy = pos_j.y - pos_i.y;
            double L = Mathf.Sqrt((float)(dx * dx + dy * dy));
            elementLengths[e] = L;

            // 1. Calculate Self-Weight for this beam
            // Volume = Area * Length. Mass = Density * Volume.
            // Force = Mass * Gravity (-9.81).
            double beamMass = density * crossSectionArea * L;
            double beamWeight = beamMass * -9.81;
            
            // Distribute half the weight to each node (Lumped Mass approach)
            F[2 * ni + 1] += beamWeight / 2.0;
            F[2 * nj + 1] += beamWeight / 2.0;

            // 2. Stiffness Matrix Calculation
            double cx = dx / L;
            double cy = dy / L;
            double stiffness = crossSectionArea * youngsModulus / L;

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

        double[] u_f = Matrix.Solve(K_ff, F_f);
        
        if (u_f == null) // Solver failed (Unstable)
        {
            return new AnalysisResult { IsStable = false };
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

            double L = elementLengths[e];
            double dx = nodePositions[nj].x - nodePositions[ni].x;
            double dy = nodePositions[nj].y - nodePositions[ni].y;
            double cx = dx / L;
            double cy = dy / L;

            var ue = new double[] { u[2*ni], u[2*ni+1], u[2*nj], u[2*nj+1] };
            
            // Axial Force: F = (EA/L) * ChangeInLength
            double force = (crossSectionArea * youngsModulus / L) * (-cx * ue[0] - cy * ue[1] + cx * ue[2] + cy * ue[3]);
            memberForces[e] = (float)force;
            
            // Calculate Stress Percentage
            float maxTensileForce = (float)(crossSectionArea * yieldStress);
            
            // Euler Buckling Critical Load (P_cr = pi^2 * E * I / L^2)
            // Assuming square cross section: I = a^4 / 12 = A^2 / 12
            float momentOfInertia = (float) (Mathf.Pow(Mathf.Sqrt(crossSectionArea), 4) / 12.0f);
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