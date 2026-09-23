Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Per Neuropil Connection And Synapse Counts [Original Version Used Prior To July 2025]:
    ''' ``neuropil_synapse_table.csv.gz`` (134,181 rows, 321 columns).
    ''' 
    ''' In-out synapse &amp; partner counts by neuropil. For every cell and neuropil
    ''' (region), contains the number of input and output synapses, as well as the
    ''' number of input and output partners the cell has in that neuropil.
    ''' </summary>
    ''' <remarks>
    ''' Note: this resource is a convenience, it can be derived from the connectivity
    ''' table.
    ''' 
    ''' The count columns are declared in the column order of the original csv document,
    ''' that is from col 2 to col 321, because several column names of this table
    ''' contain blank characters (for example ``input synapses in AL_L``), the
    ''' <see cref="ColumnAttribute"/> alias is required for every of them.
    ''' </remarks>
    Public Class NeuropilSynapseTable

        ''' <summary>
        ''' FlyWire Root ID of the cell.
        ''' </summary>
        <Column("root_id")>
        Public Property RootId As Long

        <Column("input synapses")>
        Public Property InputSynapses As Double

        <Column("input partners")>
        Public Property InputPartners As Double

        <Column("output synapses")>
        Public Property OutputSynapses As Double

        <Column("output partners")>
        Public Property OutputPartners As Double

        <Column("input synapses in AL_L")>
        Public Property InputSynapsesInAL_L As Double

        <Column("input synapses in AL_R")>
        Public Property InputSynapsesInAL_R As Double

        <Column("input synapses in AME_L")>
        Public Property InputSynapsesInAME_L As Double

        <Column("input synapses in AME_R")>
        Public Property InputSynapsesInAME_R As Double

        <Column("input synapses in AMMC_L")>
        Public Property InputSynapsesInAMMC_L As Double

        <Column("input synapses in AMMC_R")>
        Public Property InputSynapsesInAMMC_R As Double

        <Column("input synapses in AOTU_L")>
        Public Property InputSynapsesInAOTU_L As Double

        <Column("input synapses in AOTU_R")>
        Public Property InputSynapsesInAOTU_R As Double

        <Column("input synapses in ATL_L")>
        Public Property InputSynapsesInATL_L As Double

        <Column("input synapses in ATL_R")>
        Public Property InputSynapsesInATL_R As Double

        <Column("input synapses in AVLP_L")>
        Public Property InputSynapsesInAVLP_L As Double

        <Column("input synapses in AVLP_R")>
        Public Property InputSynapsesInAVLP_R As Double

        <Column("input synapses in BU_L")>
        Public Property InputSynapsesInBU_L As Double

        <Column("input synapses in BU_R")>
        Public Property InputSynapsesInBU_R As Double

        <Column("input synapses in CAN_L")>
        Public Property InputSynapsesInCAN_L As Double

        <Column("input synapses in CAN_R")>
        Public Property InputSynapsesInCAN_R As Double

        <Column("input synapses in CRE_L")>
        Public Property InputSynapsesInCRE_L As Double

        <Column("input synapses in CRE_R")>
        Public Property InputSynapsesInCRE_R As Double

        <Column("input synapses in EB")>
        Public Property InputSynapsesInEB As Double

        <Column("input synapses in EPA_L")>
        Public Property InputSynapsesInEPA_L As Double

        <Column("input synapses in EPA_R")>
        Public Property InputSynapsesInEPA_R As Double

        <Column("input synapses in FB")>
        Public Property InputSynapsesInFB As Double

        <Column("input synapses in FLA_L")>
        Public Property InputSynapsesInFLA_L As Double

        <Column("input synapses in FLA_R")>
        Public Property InputSynapsesInFLA_R As Double

        <Column("input synapses in GA_L")>
        Public Property InputSynapsesInGA_L As Double

        <Column("input synapses in GA_R")>
        Public Property InputSynapsesInGA_R As Double

        <Column("input synapses in GNG")>
        Public Property InputSynapsesInGNG As Double

        <Column("input synapses in GOR_L")>
        Public Property InputSynapsesInGOR_L As Double

        <Column("input synapses in GOR_R")>
        Public Property InputSynapsesInGOR_R As Double

        <Column("input synapses in IB_L")>
        Public Property InputSynapsesInIB_L As Double

        <Column("input synapses in IB_R")>
        Public Property InputSynapsesInIB_R As Double

        <Column("input synapses in ICL_L")>
        Public Property InputSynapsesInICL_L As Double

        <Column("input synapses in ICL_R")>
        Public Property InputSynapsesInICL_R As Double

        <Column("input synapses in IPS_L")>
        Public Property InputSynapsesInIPS_L As Double

        <Column("input synapses in IPS_R")>
        Public Property InputSynapsesInIPS_R As Double

        <Column("input synapses in LAL_L")>
        Public Property InputSynapsesInLAL_L As Double

        <Column("input synapses in LAL_R")>
        Public Property InputSynapsesInLAL_R As Double

        <Column("input synapses in LA_L")>
        Public Property InputSynapsesInLA_L As Double

        <Column("input synapses in LA_R")>
        Public Property InputSynapsesInLA_R As Double

        <Column("input synapses in LH_L")>
        Public Property InputSynapsesInLH_L As Double

        <Column("input synapses in LH_R")>
        Public Property InputSynapsesInLH_R As Double

        <Column("input synapses in LOP_L")>
        Public Property InputSynapsesInLOP_L As Double

        <Column("input synapses in LOP_R")>
        Public Property InputSynapsesInLOP_R As Double

        <Column("input synapses in LO_L")>
        Public Property InputSynapsesInLO_L As Double

        <Column("input synapses in LO_R")>
        Public Property InputSynapsesInLO_R As Double

        <Column("input synapses in MB_CA_L")>
        Public Property InputSynapsesInMB_CA_L As Double

        <Column("input synapses in MB_CA_R")>
        Public Property InputSynapsesInMB_CA_R As Double

        <Column("input synapses in MB_ML_L")>
        Public Property InputSynapsesInMB_ML_L As Double

        <Column("input synapses in MB_ML_R")>
        Public Property InputSynapsesInMB_ML_R As Double

        <Column("input synapses in MB_PED_L")>
        Public Property InputSynapsesInMB_PED_L As Double

        <Column("input synapses in MB_PED_R")>
        Public Property InputSynapsesInMB_PED_R As Double

        <Column("input synapses in MB_VL_L")>
        Public Property InputSynapsesInMB_VL_L As Double

        <Column("input synapses in MB_VL_R")>
        Public Property InputSynapsesInMB_VL_R As Double

        <Column("input synapses in ME_L")>
        Public Property InputSynapsesInME_L As Double

        <Column("input synapses in ME_R")>
        Public Property InputSynapsesInME_R As Double

        <Column("input synapses in NO")>
        Public Property InputSynapsesInNO As Double

        <Column("input synapses in OCG")>
        Public Property InputSynapsesInOCG As Double

        <Column("input synapses in PB")>
        Public Property InputSynapsesInPB As Double

        <Column("input synapses in PLP_L")>
        Public Property InputSynapsesInPLP_L As Double

        <Column("input synapses in PLP_R")>
        Public Property InputSynapsesInPLP_R As Double

        <Column("input synapses in PRW")>
        Public Property InputSynapsesInPRW As Double

        <Column("input synapses in PVLP_L")>
        Public Property InputSynapsesInPVLP_L As Double

        <Column("input synapses in PVLP_R")>
        Public Property InputSynapsesInPVLP_R As Double

        <Column("input synapses in SAD")>
        Public Property InputSynapsesInSAD As Double

        <Column("input synapses in SCL_L")>
        Public Property InputSynapsesInSCL_L As Double

        <Column("input synapses in SCL_R")>
        Public Property InputSynapsesInSCL_R As Double

        <Column("input synapses in SIP_L")>
        Public Property InputSynapsesInSIP_L As Double

        <Column("input synapses in SIP_R")>
        Public Property InputSynapsesInSIP_R As Double

        <Column("input synapses in SLP_L")>
        Public Property InputSynapsesInSLP_L As Double

        <Column("input synapses in SLP_R")>
        Public Property InputSynapsesInSLP_R As Double

        <Column("input synapses in SMP_L")>
        Public Property InputSynapsesInSMP_L As Double

        <Column("input synapses in SMP_R")>
        Public Property InputSynapsesInSMP_R As Double

        <Column("input synapses in SPS_L")>
        Public Property InputSynapsesInSPS_L As Double

        <Column("input synapses in SPS_R")>
        Public Property InputSynapsesInSPS_R As Double

        <Column("input synapses in UNASGD")>
        Public Property InputSynapsesInUNASGD As Double

        <Column("input synapses in VES_L")>
        Public Property InputSynapsesInVES_L As Double

        <Column("input synapses in VES_R")>
        Public Property InputSynapsesInVES_R As Double

        <Column("input synapses in WED_L")>
        Public Property InputSynapsesInWED_L As Double

        <Column("input synapses in WED_R")>
        Public Property InputSynapsesInWED_R As Double

        <Column("input partners in AL_L")>
        Public Property InputPartnersInAL_L As Double

        <Column("input partners in AL_R")>
        Public Property InputPartnersInAL_R As Double

        <Column("input partners in AME_L")>
        Public Property InputPartnersInAME_L As Double

        <Column("input partners in AME_R")>
        Public Property InputPartnersInAME_R As Double

        <Column("input partners in AMMC_L")>
        Public Property InputPartnersInAMMC_L As Double

        <Column("input partners in AMMC_R")>
        Public Property InputPartnersInAMMC_R As Double

        <Column("input partners in AOTU_L")>
        Public Property InputPartnersInAOTU_L As Double

        <Column("input partners in AOTU_R")>
        Public Property InputPartnersInAOTU_R As Double

        <Column("input partners in ATL_L")>
        Public Property InputPartnersInATL_L As Double

        <Column("input partners in ATL_R")>
        Public Property InputPartnersInATL_R As Double

        <Column("input partners in AVLP_L")>
        Public Property InputPartnersInAVLP_L As Double

        <Column("input partners in AVLP_R")>
        Public Property InputPartnersInAVLP_R As Double

        <Column("input partners in BU_L")>
        Public Property InputPartnersInBU_L As Double

        <Column("input partners in BU_R")>
        Public Property InputPartnersInBU_R As Double

        <Column("input partners in CAN_L")>
        Public Property InputPartnersInCAN_L As Double

        <Column("input partners in CAN_R")>
        Public Property InputPartnersInCAN_R As Double

        <Column("input partners in CRE_L")>
        Public Property InputPartnersInCRE_L As Double

        <Column("input partners in CRE_R")>
        Public Property InputPartnersInCRE_R As Double

        <Column("input partners in EB")>
        Public Property InputPartnersInEB As Double

        <Column("input partners in EPA_L")>
        Public Property InputPartnersInEPA_L As Double

        <Column("input partners in EPA_R")>
        Public Property InputPartnersInEPA_R As Double

        <Column("input partners in FB")>
        Public Property InputPartnersInFB As Double

        <Column("input partners in FLA_L")>
        Public Property InputPartnersInFLA_L As Double

        <Column("input partners in FLA_R")>
        Public Property InputPartnersInFLA_R As Double

        <Column("input partners in GA_L")>
        Public Property InputPartnersInGA_L As Double

        <Column("input partners in GA_R")>
        Public Property InputPartnersInGA_R As Double

        <Column("input partners in GNG")>
        Public Property InputPartnersInGNG As Double

        <Column("input partners in GOR_L")>
        Public Property InputPartnersInGOR_L As Double

        <Column("input partners in GOR_R")>
        Public Property InputPartnersInGOR_R As Double

        <Column("input partners in IB_L")>
        Public Property InputPartnersInIB_L As Double

        <Column("input partners in IB_R")>
        Public Property InputPartnersInIB_R As Double

        <Column("input partners in ICL_L")>
        Public Property InputPartnersInICL_L As Double

        <Column("input partners in ICL_R")>
        Public Property InputPartnersInICL_R As Double

        <Column("input partners in IPS_L")>
        Public Property InputPartnersInIPS_L As Double

        <Column("input partners in IPS_R")>
        Public Property InputPartnersInIPS_R As Double

        <Column("input partners in LAL_L")>
        Public Property InputPartnersInLAL_L As Double

        <Column("input partners in LAL_R")>
        Public Property InputPartnersInLAL_R As Double

        <Column("input partners in LA_L")>
        Public Property InputPartnersInLA_L As Double

        <Column("input partners in LA_R")>
        Public Property InputPartnersInLA_R As Double

        <Column("input partners in LH_L")>
        Public Property InputPartnersInLH_L As Double

        <Column("input partners in LH_R")>
        Public Property InputPartnersInLH_R As Double

        <Column("input partners in LOP_L")>
        Public Property InputPartnersInLOP_L As Double

        <Column("input partners in LOP_R")>
        Public Property InputPartnersInLOP_R As Double

        <Column("input partners in LO_L")>
        Public Property InputPartnersInLO_L As Double

        <Column("input partners in LO_R")>
        Public Property InputPartnersInLO_R As Double

        <Column("input partners in MB_CA_L")>
        Public Property InputPartnersInMB_CA_L As Double

        <Column("input partners in MB_CA_R")>
        Public Property InputPartnersInMB_CA_R As Double

        <Column("input partners in MB_ML_L")>
        Public Property InputPartnersInMB_ML_L As Double

        <Column("input partners in MB_ML_R")>
        Public Property InputPartnersInMB_ML_R As Double

        <Column("input partners in MB_PED_L")>
        Public Property InputPartnersInMB_PED_L As Double

        <Column("input partners in MB_PED_R")>
        Public Property InputPartnersInMB_PED_R As Double

        <Column("input partners in MB_VL_L")>
        Public Property InputPartnersInMB_VL_L As Double

        <Column("input partners in MB_VL_R")>
        Public Property InputPartnersInMB_VL_R As Double

        <Column("input partners in ME_L")>
        Public Property InputPartnersInME_L As Double

        <Column("input partners in ME_R")>
        Public Property InputPartnersInME_R As Double

        <Column("input partners in NO")>
        Public Property InputPartnersInNO As Double

        <Column("input partners in OCG")>
        Public Property InputPartnersInOCG As Double

        <Column("input partners in PB")>
        Public Property InputPartnersInPB As Double

        <Column("input partners in PLP_L")>
        Public Property InputPartnersInPLP_L As Double

        <Column("input partners in PLP_R")>
        Public Property InputPartnersInPLP_R As Double

        <Column("input partners in PRW")>
        Public Property InputPartnersInPRW As Double

        <Column("input partners in PVLP_L")>
        Public Property InputPartnersInPVLP_L As Double

        <Column("input partners in PVLP_R")>
        Public Property InputPartnersInPVLP_R As Double

        <Column("input partners in SAD")>
        Public Property InputPartnersInSAD As Double

        <Column("input partners in SCL_L")>
        Public Property InputPartnersInSCL_L As Double

        <Column("input partners in SCL_R")>
        Public Property InputPartnersInSCL_R As Double

        <Column("input partners in SIP_L")>
        Public Property InputPartnersInSIP_L As Double

        <Column("input partners in SIP_R")>
        Public Property InputPartnersInSIP_R As Double

        <Column("input partners in SLP_L")>
        Public Property InputPartnersInSLP_L As Double

        <Column("input partners in SLP_R")>
        Public Property InputPartnersInSLP_R As Double

        <Column("input partners in SMP_L")>
        Public Property InputPartnersInSMP_L As Double

        <Column("input partners in SMP_R")>
        Public Property InputPartnersInSMP_R As Double

        <Column("input partners in SPS_L")>
        Public Property InputPartnersInSPS_L As Double

        <Column("input partners in SPS_R")>
        Public Property InputPartnersInSPS_R As Double

        <Column("input partners in UNASGD")>
        Public Property InputPartnersInUNASGD As Double

        <Column("input partners in VES_L")>
        Public Property InputPartnersInVES_L As Double

        <Column("input partners in VES_R")>
        Public Property InputPartnersInVES_R As Double

        <Column("input partners in WED_L")>
        Public Property InputPartnersInWED_L As Double

        <Column("input partners in WED_R")>
        Public Property InputPartnersInWED_R As Double

        <Column("output synapses in AL_L")>
        Public Property OutputSynapsesInAL_L As Double

        <Column("output synapses in AL_R")>
        Public Property OutputSynapsesInAL_R As Double

        <Column("output synapses in AME_L")>
        Public Property OutputSynapsesInAME_L As Double

        <Column("output synapses in AME_R")>
        Public Property OutputSynapsesInAME_R As Double

        <Column("output synapses in AMMC_L")>
        Public Property OutputSynapsesInAMMC_L As Double

        <Column("output synapses in AMMC_R")>
        Public Property OutputSynapsesInAMMC_R As Double

        <Column("output synapses in AOTU_L")>
        Public Property OutputSynapsesInAOTU_L As Double

        <Column("output synapses in AOTU_R")>
        Public Property OutputSynapsesInAOTU_R As Double

        <Column("output synapses in ATL_L")>
        Public Property OutputSynapsesInATL_L As Double

        <Column("output synapses in ATL_R")>
        Public Property OutputSynapsesInATL_R As Double

        <Column("output synapses in AVLP_L")>
        Public Property OutputSynapsesInAVLP_L As Double

        <Column("output synapses in AVLP_R")>
        Public Property OutputSynapsesInAVLP_R As Double

        <Column("output synapses in BU_L")>
        Public Property OutputSynapsesInBU_L As Double

        <Column("output synapses in BU_R")>
        Public Property OutputSynapsesInBU_R As Double

        <Column("output synapses in CAN_L")>
        Public Property OutputSynapsesInCAN_L As Double

        <Column("output synapses in CAN_R")>
        Public Property OutputSynapsesInCAN_R As Double

        <Column("output synapses in CRE_L")>
        Public Property OutputSynapsesInCRE_L As Double

        <Column("output synapses in CRE_R")>
        Public Property OutputSynapsesInCRE_R As Double

        <Column("output synapses in EB")>
        Public Property OutputSynapsesInEB As Double

        <Column("output synapses in EPA_L")>
        Public Property OutputSynapsesInEPA_L As Double

        <Column("output synapses in EPA_R")>
        Public Property OutputSynapsesInEPA_R As Double

        <Column("output synapses in FB")>
        Public Property OutputSynapsesInFB As Double

        <Column("output synapses in FLA_L")>
        Public Property OutputSynapsesInFLA_L As Double

        <Column("output synapses in FLA_R")>
        Public Property OutputSynapsesInFLA_R As Double

        <Column("output synapses in GA_L")>
        Public Property OutputSynapsesInGA_L As Double

        <Column("output synapses in GA_R")>
        Public Property OutputSynapsesInGA_R As Double

        <Column("output synapses in GNG")>
        Public Property OutputSynapsesInGNG As Double

        <Column("output synapses in GOR_L")>
        Public Property OutputSynapsesInGOR_L As Double

        <Column("output synapses in GOR_R")>
        Public Property OutputSynapsesInGOR_R As Double

        <Column("output synapses in IB_L")>
        Public Property OutputSynapsesInIB_L As Double

        <Column("output synapses in IB_R")>
        Public Property OutputSynapsesInIB_R As Double

        <Column("output synapses in ICL_L")>
        Public Property OutputSynapsesInICL_L As Double

        <Column("output synapses in ICL_R")>
        Public Property OutputSynapsesInICL_R As Double

        <Column("output synapses in IPS_L")>
        Public Property OutputSynapsesInIPS_L As Double

        <Column("output synapses in IPS_R")>
        Public Property OutputSynapsesInIPS_R As Double

        <Column("output synapses in LAL_L")>
        Public Property OutputSynapsesInLAL_L As Double

        <Column("output synapses in LAL_R")>
        Public Property OutputSynapsesInLAL_R As Double

        <Column("output synapses in LA_L")>
        Public Property OutputSynapsesInLA_L As Double

        <Column("output synapses in LA_R")>
        Public Property OutputSynapsesInLA_R As Double

        <Column("output synapses in LH_L")>
        Public Property OutputSynapsesInLH_L As Double

        <Column("output synapses in LH_R")>
        Public Property OutputSynapsesInLH_R As Double

        <Column("output synapses in LOP_L")>
        Public Property OutputSynapsesInLOP_L As Double

        <Column("output synapses in LOP_R")>
        Public Property OutputSynapsesInLOP_R As Double

        <Column("output synapses in LO_L")>
        Public Property OutputSynapsesInLO_L As Double

        <Column("output synapses in LO_R")>
        Public Property OutputSynapsesInLO_R As Double

        <Column("output synapses in MB_CA_L")>
        Public Property OutputSynapsesInMB_CA_L As Double

        <Column("output synapses in MB_CA_R")>
        Public Property OutputSynapsesInMB_CA_R As Double

        <Column("output synapses in MB_ML_L")>
        Public Property OutputSynapsesInMB_ML_L As Double

        <Column("output synapses in MB_ML_R")>
        Public Property OutputSynapsesInMB_ML_R As Double

        <Column("output synapses in MB_PED_L")>
        Public Property OutputSynapsesInMB_PED_L As Double

        <Column("output synapses in MB_PED_R")>
        Public Property OutputSynapsesInMB_PED_R As Double

        <Column("output synapses in MB_VL_L")>
        Public Property OutputSynapsesInMB_VL_L As Double

        <Column("output synapses in MB_VL_R")>
        Public Property OutputSynapsesInMB_VL_R As Double

        <Column("output synapses in ME_L")>
        Public Property OutputSynapsesInME_L As Double

        <Column("output synapses in ME_R")>
        Public Property OutputSynapsesInME_R As Double

        <Column("output synapses in NO")>
        Public Property OutputSynapsesInNO As Double

        <Column("output synapses in OCG")>
        Public Property OutputSynapsesInOCG As Double

        <Column("output synapses in PB")>
        Public Property OutputSynapsesInPB As Double

        <Column("output synapses in PLP_L")>
        Public Property OutputSynapsesInPLP_L As Double

        <Column("output synapses in PLP_R")>
        Public Property OutputSynapsesInPLP_R As Double

        <Column("output synapses in PRW")>
        Public Property OutputSynapsesInPRW As Double

        <Column("output synapses in PVLP_L")>
        Public Property OutputSynapsesInPVLP_L As Double

        <Column("output synapses in PVLP_R")>
        Public Property OutputSynapsesInPVLP_R As Double

        <Column("output synapses in SAD")>
        Public Property OutputSynapsesInSAD As Double

        <Column("output synapses in SCL_L")>
        Public Property OutputSynapsesInSCL_L As Double

        <Column("output synapses in SCL_R")>
        Public Property OutputSynapsesInSCL_R As Double

        <Column("output synapses in SIP_L")>
        Public Property OutputSynapsesInSIP_L As Double

        <Column("output synapses in SIP_R")>
        Public Property OutputSynapsesInSIP_R As Double

        <Column("output synapses in SLP_L")>
        Public Property OutputSynapsesInSLP_L As Double

        <Column("output synapses in SLP_R")>
        Public Property OutputSynapsesInSLP_R As Double

        <Column("output synapses in SMP_L")>
        Public Property OutputSynapsesInSMP_L As Double

        <Column("output synapses in SMP_R")>
        Public Property OutputSynapsesInSMP_R As Double

        <Column("output synapses in SPS_L")>
        Public Property OutputSynapsesInSPS_L As Double

        <Column("output synapses in SPS_R")>
        Public Property OutputSynapsesInSPS_R As Double

        <Column("output synapses in UNASGD")>
        Public Property OutputSynapsesInUNASGD As Double

        <Column("output synapses in VES_L")>
        Public Property OutputSynapsesInVES_L As Double

        <Column("output synapses in VES_R")>
        Public Property OutputSynapsesInVES_R As Double

        <Column("output synapses in WED_L")>
        Public Property OutputSynapsesInWED_L As Double

        <Column("output synapses in WED_R")>
        Public Property OutputSynapsesInWED_R As Double

        <Column("output partners in AL_L")>
        Public Property OutputPartnersInAL_L As Double

        <Column("output partners in AL_R")>
        Public Property OutputPartnersInAL_R As Double

        <Column("output partners in AME_L")>
        Public Property OutputPartnersInAME_L As Double

        <Column("output partners in AME_R")>
        Public Property OutputPartnersInAME_R As Double

        <Column("output partners in AMMC_L")>
        Public Property OutputPartnersInAMMC_L As Double

        <Column("output partners in AMMC_R")>
        Public Property OutputPartnersInAMMC_R As Double

        <Column("output partners in AOTU_L")>
        Public Property OutputPartnersInAOTU_L As Double

        <Column("output partners in AOTU_R")>
        Public Property OutputPartnersInAOTU_R As Double

        <Column("output partners in ATL_L")>
        Public Property OutputPartnersInATL_L As Double

        <Column("output partners in ATL_R")>
        Public Property OutputPartnersInATL_R As Double

        <Column("output partners in AVLP_L")>
        Public Property OutputPartnersInAVLP_L As Double

        <Column("output partners in AVLP_R")>
        Public Property OutputPartnersInAVLP_R As Double

        <Column("output partners in BU_L")>
        Public Property OutputPartnersInBU_L As Double

        <Column("output partners in BU_R")>
        Public Property OutputPartnersInBU_R As Double

        <Column("output partners in CAN_L")>
        Public Property OutputPartnersInCAN_L As Double

        <Column("output partners in CAN_R")>
        Public Property OutputPartnersInCAN_R As Double

        <Column("output partners in CRE_L")>
        Public Property OutputPartnersInCRE_L As Double

        <Column("output partners in CRE_R")>
        Public Property OutputPartnersInCRE_R As Double

        <Column("output partners in EB")>
        Public Property OutputPartnersInEB As Double

        <Column("output partners in EPA_L")>
        Public Property OutputPartnersInEPA_L As Double

        <Column("output partners in EPA_R")>
        Public Property OutputPartnersInEPA_R As Double

        <Column("output partners in FB")>
        Public Property OutputPartnersInFB As Double

        <Column("output partners in FLA_L")>
        Public Property OutputPartnersInFLA_L As Double

        <Column("output partners in FLA_R")>
        Public Property OutputPartnersInFLA_R As Double

        <Column("output partners in GA_L")>
        Public Property OutputPartnersInGA_L As Double

        <Column("output partners in GA_R")>
        Public Property OutputPartnersInGA_R As Double

        <Column("output partners in GNG")>
        Public Property OutputPartnersInGNG As Double

        <Column("output partners in GOR_L")>
        Public Property OutputPartnersInGOR_L As Double

        <Column("output partners in GOR_R")>
        Public Property OutputPartnersInGOR_R As Double

        <Column("output partners in IB_L")>
        Public Property OutputPartnersInIB_L As Double

        <Column("output partners in IB_R")>
        Public Property OutputPartnersInIB_R As Double

        <Column("output partners in ICL_L")>
        Public Property OutputPartnersInICL_L As Double

        <Column("output partners in ICL_R")>
        Public Property OutputPartnersInICL_R As Double

        <Column("output partners in IPS_L")>
        Public Property OutputPartnersInIPS_L As Double

        <Column("output partners in IPS_R")>
        Public Property OutputPartnersInIPS_R As Double

        <Column("output partners in LAL_L")>
        Public Property OutputPartnersInLAL_L As Double

        <Column("output partners in LAL_R")>
        Public Property OutputPartnersInLAL_R As Double

        <Column("output partners in LA_L")>
        Public Property OutputPartnersInLA_L As Double

        <Column("output partners in LA_R")>
        Public Property OutputPartnersInLA_R As Double

        <Column("output partners in LH_L")>
        Public Property OutputPartnersInLH_L As Double

        <Column("output partners in LH_R")>
        Public Property OutputPartnersInLH_R As Double

        <Column("output partners in LOP_L")>
        Public Property OutputPartnersInLOP_L As Double

        <Column("output partners in LOP_R")>
        Public Property OutputPartnersInLOP_R As Double

        <Column("output partners in LO_L")>
        Public Property OutputPartnersInLO_L As Double

        <Column("output partners in LO_R")>
        Public Property OutputPartnersInLO_R As Double

        <Column("output partners in MB_CA_L")>
        Public Property OutputPartnersInMB_CA_L As Double

        <Column("output partners in MB_CA_R")>
        Public Property OutputPartnersInMB_CA_R As Double

        <Column("output partners in MB_ML_L")>
        Public Property OutputPartnersInMB_ML_L As Double

        <Column("output partners in MB_ML_R")>
        Public Property OutputPartnersInMB_ML_R As Double

        <Column("output partners in MB_PED_L")>
        Public Property OutputPartnersInMB_PED_L As Double

        <Column("output partners in MB_PED_R")>
        Public Property OutputPartnersInMB_PED_R As Double

        <Column("output partners in MB_VL_L")>
        Public Property OutputPartnersInMB_VL_L As Double

        <Column("output partners in MB_VL_R")>
        Public Property OutputPartnersInMB_VL_R As Double

        <Column("output partners in ME_L")>
        Public Property OutputPartnersInME_L As Double

        <Column("output partners in ME_R")>
        Public Property OutputPartnersInME_R As Double

        <Column("output partners in NO")>
        Public Property OutputPartnersInNO As Double

        <Column("output partners in OCG")>
        Public Property OutputPartnersInOCG As Double

        <Column("output partners in PB")>
        Public Property OutputPartnersInPB As Double

        <Column("output partners in PLP_L")>
        Public Property OutputPartnersInPLP_L As Double

        <Column("output partners in PLP_R")>
        Public Property OutputPartnersInPLP_R As Double

        <Column("output partners in PRW")>
        Public Property OutputPartnersInPRW As Double

        <Column("output partners in PVLP_L")>
        Public Property OutputPartnersInPVLP_L As Double

        <Column("output partners in PVLP_R")>
        Public Property OutputPartnersInPVLP_R As Double

        <Column("output partners in SAD")>
        Public Property OutputPartnersInSAD As Double

        <Column("output partners in SCL_L")>
        Public Property OutputPartnersInSCL_L As Double

        <Column("output partners in SCL_R")>
        Public Property OutputPartnersInSCL_R As Double

        <Column("output partners in SIP_L")>
        Public Property OutputPartnersInSIP_L As Double

        <Column("output partners in SIP_R")>
        Public Property OutputPartnersInSIP_R As Double

        <Column("output partners in SLP_L")>
        Public Property OutputPartnersInSLP_L As Double

        <Column("output partners in SLP_R")>
        Public Property OutputPartnersInSLP_R As Double

        <Column("output partners in SMP_L")>
        Public Property OutputPartnersInSMP_L As Double

        <Column("output partners in SMP_R")>
        Public Property OutputPartnersInSMP_R As Double

        <Column("output partners in SPS_L")>
        Public Property OutputPartnersInSPS_L As Double

        <Column("output partners in SPS_R")>
        Public Property OutputPartnersInSPS_R As Double

        <Column("output partners in UNASGD")>
        Public Property OutputPartnersInUNASGD As Double

        <Column("output partners in VES_L")>
        Public Property OutputPartnersInVES_L As Double

        <Column("output partners in VES_R")>
        Public Property OutputPartnersInVES_R As Double

        <Column("output partners in WED_L")>
        Public Property OutputPartnersInWED_L As Double

        <Column("output partners in WED_R")>
        Public Property OutputPartnersInWED_R As Double

    End Class
End Namespace
