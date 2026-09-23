Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Synapse Attachment Rates [Original Version Used Prior To July 2025]:
    ''' ``synapse_attachment_rates.csv.gz`` (5 columns).
    ''' 
    ''' Pre/post synapse attachment rates to proofread neurons, by neuropil.
    ''' </summary>
    ''' <remarks>
    ''' The schema of this table is defined by the header line of the original csv
    ''' document, ie. ``neuropil, count_total, count_proof, proof_ratio, side``.
    ''' </remarks>
    Public Class SynapseAttachmentRates

        <Column("neuropil")>
        Public Property Neuropil As String

        <Column("count_total")>
        Public Property CountTotal As Double

        <Column("count_proof")>
        Public Property CountProof As Double

        <Column("proof_ratio")>
        Public Property ProofRatio As Double

        ''' <summary>
        ''' Pre or post synaptic site, ``pre`` / ``post``.
        ''' </summary>
        <Column("side")>
        Public Property Side As String

    End Class
End Namespace
