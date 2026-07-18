namespace Olden_Era___Template_Editor.Models
{
    /// <summary>
    /// Algorithms available for automatically placing zones on the canvas after import.
    /// The force-directed embedding is the historical default; the remaining members
    /// are graph-theoretic / network-logic alternatives offered in the import window.
    /// </summary>
    public enum ImportLayoutAlgorithm
    {
        /// <summary>Fruchterman-Reingold spring embedder (historical default).</summary>
        Force,

        /// <summary>Spectral embedding using the graph Laplacian eigenvectors.</summary>
        Spectral,

        /// <summary>Classical multidimensional scaling on shortest-path distances.</summary>
        Mds,

        /// <summary>Radial tree (hierarchical) layout over a minimum spanning tree.</summary>
        Hierarchical,

        /// <summary>Network-logic layout: zones placed on concentric rings by combined
        /// PageRank + Betweenness centrality (most central nearest the centre).</summary>
        Centrality,

        /// <summary>Network-logic layout: a layered radial flow that clusters tightly
        /// connected zones together and pushes peripheral chains outward, so the map
        /// reads like a network diagram rather than a geometric embedding.</summary>
        Network
    }
}
