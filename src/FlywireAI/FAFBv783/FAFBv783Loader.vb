Imports System.Runtime.CompilerServices
Imports System.Text
Imports Microsoft.VisualBasic.Data.Framework
Imports Microsoft.VisualBasic.Data.Framework.IO
Imports Microsoft.VisualBasic.Data.Framework.IO.Linq
Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' FAFB v783 数据集的集中加载模块，表格的字段定义请参考 ``docs/fafb-v783-data.md``。
    ''' 
    ''' **全量加载**：``LoadXxx`` 系列函数通过 csv 反射存储提供者的 <c>LoadCsv(Of T)</c> 扩展方法
    ''' 将整个 csv 文档反序列化为 <see cref="List(Of T)"/>，仅适合用于行数较少的表格。
    ''' 
    ''' **流式加载**：``StreamXxx`` 系列函数通过 ``OpenHandle`` + ``AsLinq`` 以惰性迭代的方式逐行
    ''' 读取数据，内存占用为 O(1)，适合用于千万行级别的超大 csv 表格。
    ''' 
    ''' **原始行读取**：<see cref="OpenRawHandle"/> 与 <see cref="StreamRaw"/> 用于读取没有定义数据模型
    ''' 的 csv 文件。
    ''' </summary>
    ''' <remarks>
    ''' 注意：``sk_lod1_783_healed.zip`` (Neuron Skeletons) 是 SWC 骨架的 zip 归档文件，不是表格数据，
    ''' 因此没有定义对应的数据模型类型。
    ''' </remarks>
    Public Module FAFBv783Loader

        ''' <summary>
        ''' 以惰性迭代的方式打开 csv 文件句柄，并且逐行解析为数据模型对象。
        ''' 
        ''' (``DataLinqStream.OpenHandle`` + ``DataLinqStream.AsLinq``，这两个函数都定义在
        ''' ``Data\DataFrame\Linq\DataStream.vb`` 文件之中)
        ''' </summary>
        ''' <typeparam name="T">
        ''' 数据模型类型，必须具有公共无参构造函数。
        ''' </typeparam>
        ''' <param name="path">csv 文件所在的文件路径。</param>
        ''' <param name="encoding">文本编码，默认使用 <see cref="Encodings.Default"/>。</param>
        ''' <param name="parallel">是否以并行 Linq 的方式读取数据行。</param>
        ''' <remarks>
        ''' ### 为什么没有使用 <see cref="DataStream"/> 类 ?
        ''' 
        ''' ``DataStream.OpenHandle`` + ``DataStream.AsLinq`` 这一对函数在实测之中存在一个框架缺陷：
        ''' 
        ''' ``DataStream`` 的构造函数已经通过 ``ReadLine`` 读取了标题行，但是 ``BufferProvider`` 在
        ''' 解析数据之前调用了 ``_file.BaseStream.Seek(0, Begin)`` 重新定位底层文件流，却没有同时调用
        ''' ``StreamReader.DiscardBufferedData()`` 丢弃 reader 内部已经缓冲的文本，于是:
        ''' 
        ''' 1. 缓冲区之中的数据行会被重复读取一次 (实测 ``names.csv`` 返回 139,282 行，实际只有 139,255 行)；
        ''' 2. 并且 ``DataStream.Dispose`` 并不会关闭内部的 ``StreamReader``，导致文件句柄一直被占用
        '''    (再次打开相同的文件会抛出 ``IOException``)。
        ''' 
        ''' 而 ``DataLinqStream.OpenHandle`` 所返回的数据行是 ``IterateAllLines().Skip(1)``，即标题行之后
        ''' 的完整的文本行序列，不存在重复读取的问题，并且底层的 <see cref="StreamReader"/> 由迭代器
        ''' 的 ``Using`` 语句负责释放，因此在这里使用这个 api 来加载超大的 csv 文件。
        ''' </remarks>
        <Extension>
        Public Function OpenDataStream(Of T As {New, Class})(path$,
                                                            Optional encoding As Encoding = Nothing,
                                                            Optional parallel As Boolean = False) As IEnumerable(Of T)

            ' 数据行以惰性迭代的方式进行读取 (内存占用为 O(1))，迭代结束或者被提前中断的时候
            ' 由底层迭代器的 Using 语句关闭文件句柄
            Return DataLinqStream _
                .OpenHandle(path, encoding:=encoding, tqdm_wrap:=False) _
                .AsLinq(Of T)(parallel)
        End Function

        ''' <summary>
        ''' 打开没有定义数据模型的 csv 文件句柄：返回表头 schema 以及原始的数据行。
        ''' </summary>
        ''' <param name="path">csv 文件所在的文件路径。</param>
        ''' <param name="encoding">文本编码，默认使用 <see cref="Encodings.Default"/>。</param>
        ''' <param name="tsv">目标文件是否是 tsv 文档？</param>
        <Extension>
        Public Function OpenRawHandle(path$,
                                      Optional encoding As Encoding = Nothing,
                                      Optional tsv As Boolean = False) As (schema As SchemaReader, table As IEnumerable(Of RowObject))

            Return DataLinqStream.OpenHandle(path, encoding:=encoding, tsv:=tsv)
        End Function

        ''' <summary>
        ''' 以惰性迭代的方式读取没有定义数据模型的 csv 文件的原始数据行。
        ''' </summary>
        <Extension>
        Public Function StreamRaw(path$,
                                  Optional encoding As Encoding = Nothing,
                                  Optional tsv As Boolean = False) As IEnumerable(Of RowObject)

            Return path.OpenRawHandle(encoding, tsv).table
        End Function

#Region "Cell Types"

        ''' <summary>
        ''' 加载 ``consolidated_cell_types.csv`` 表格 (138,327 行)：每个细胞的 primary cell type
        ''' 以及额外的 cell type 注释。
        ''' </summary>
        <Extension>
        Public Function LoadCellTypes(path$,
                                      Optional encoding As Encoding = Nothing,
                                      Optional mute As Boolean = False) As List(Of CellTypes)

            Return path.LoadCsv(Of CellTypes)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamCellTypes(path$,
                                        Optional encoding As Encoding = Nothing,
                                        Optional parallel As Boolean = False) As IEnumerable(Of CellTypes)

            Return path.OpenDataStream(Of CellTypes)(encoding, parallel)
        End Function

#End Region

#Region "Classification"

        ''' <summary>
        ''' 加载 ``classification.csv`` 表格 (139,255 行)：层次化分类注释 (flow, super_class,
        ''' class, sub_class, hemilineage, side, nerve)。
        ''' </summary>
        <Extension>
        Public Function LoadClassification(path$,
                                           Optional encoding As Encoding = Nothing,
                                           Optional mute As Boolean = False) As List(Of Classification)

            Return path.LoadCsv(Of Classification)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamClassification(path$,
                                             Optional encoding As Encoding = Nothing,
                                             Optional parallel As Boolean = False) As IEnumerable(Of Classification)

            Return path.OpenDataStream(Of Classification)(encoding, parallel)
        End Function

#End Region

#Region "Cell Size Measurements"

        ''' <summary>
        ''' 加载 ``cell_stats.csv`` 表格 (139,246 行)：细胞的表面积、cable 长度以及体积测量值。
        ''' </summary>
        <Extension>
        Public Function LoadCellStats(path$,
                                      Optional encoding As Encoding = Nothing,
                                      Optional mute As Boolean = False) As List(Of CellStats)

            Return path.LoadCsv(Of CellStats)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamCellStats(path$,
                                        Optional encoding As Encoding = Nothing,
                                        Optional parallel As Boolean = False) As IEnumerable(Of CellStats)

            Return path.OpenDataStream(Of CellStats)(encoding, parallel)
        End Function

#End Region

#Region "Proofread Cell Names And Groups"

        ''' <summary>
        ''' 加载 ``names.csv`` 表格 (139,255 行)：细胞的名字以及细胞所属于的 group。
        ''' </summary>
        <Extension>
        Public Function LoadCellNames(path$,
                                      Optional encoding As Encoding = Nothing,
                                      Optional mute As Boolean = False) As List(Of CellNames)

            Return path.LoadCsv(Of CellNames)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamCellNames(path$,
                                        Optional encoding As Encoding = Nothing,
                                        Optional parallel As Boolean = False) As IEnumerable(Of CellNames)

            Return path.OpenDataStream(Of CellNames)(encoding, parallel)
        End Function

#End Region

#Region "Neurotransmitter Type Predictions"

        ''' <summary>
        ''' 加载 ``neurons.csv`` 表格 (139,255 行)：神经递质类型预测结果以及各个 NT 类型的预测打分。
        ''' </summary>
        <Extension>
        Public Function LoadNeurons(path$,
                                    Optional encoding As Encoding = Nothing,
                                    Optional mute As Boolean = False) As List(Of Neurons)

            Return path.LoadCsv(Of Neurons)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamNeurons(path$,
                                      Optional encoding As Encoding = Nothing,
                                      Optional parallel As Boolean = False) As IEnumerable(Of Neurons)

            Return path.OpenDataStream(Of Neurons)(encoding, parallel)
        End Function

#End Region

#Region "Visual Neuron Annotations"

        ''' <summary>
        ''' 加载 ``visual_neuron_types.csv`` 表格 (95,079 行)：视觉神经元的 cell type 以及 type family。
        ''' </summary>
        <Extension>
        Public Function LoadVisualNeuronTypes(path$,
                                              Optional encoding As Encoding = Nothing,
                                              Optional mute As Boolean = False) As List(Of VisualNeuronTypes)

            Return path.LoadCsv(Of VisualNeuronTypes)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamVisualNeuronTypes(path$,
                                                Optional encoding As Encoding = Nothing,
                                                Optional parallel As Boolean = False) As IEnumerable(Of VisualNeuronTypes)

            Return path.OpenDataStream(Of VisualNeuronTypes)(encoding, parallel)
        End Function

#End Region

#Region "Visual Neuron Columns"

        ''' <summary>
        ''' 加载 ``column_assignment.csv`` 表格 (45,528 行)：columnar 细胞类型的视觉 column 以及
        ''' 视网膜拓扑坐标 (retinotopic coordinate) 赋值。
        ''' </summary>
        <Extension>
        Public Function LoadColumnAssignment(path$,
                                             Optional encoding As Encoding = Nothing,
                                             Optional mute As Boolean = False) As List(Of ColumnAssignment)

            Return path.LoadCsv(Of ColumnAssignment)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamColumnAssignment(path$,
                                               Optional encoding As Encoding = Nothing,
                                               Optional parallel As Boolean = False) As IEnumerable(Of ColumnAssignment)

            Return path.OpenDataStream(Of ColumnAssignment)(encoding, parallel)
        End Function

#End Region

#Region "Community Labels (Raw)"

        ''' <summary>
        ''' 加载 ``labels.csv`` 表格 (160,045 行)：由 FlyWire 社区提交的原始细胞注释标签 (labels)，
        ''' 包含提交者以及所属机构的信息。
        ''' </summary>
        <Extension>
        Public Function LoadCommunityLabels(path$,
                                            Optional encoding As Encoding = Nothing,
                                            Optional mute As Boolean = False) As List(Of CommunityLabels)

            Return path.LoadCsv(Of CommunityLabels)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamCommunityLabels(path$,
                                              Optional encoding As Encoding = Nothing,
                                              Optional parallel As Boolean = False) As IEnumerable(Of CommunityLabels)

            Return path.OpenDataStream(Of CommunityLabels)(encoding, parallel)
        End Function

#End Region

#Region "Community Labels (Refined)"

        ''' <summary>
        ''' 加载 ``processed_labels.csv`` 表格 (100,091 行)：经过清洗、去重复之后的社区注释标签，
        ''' 这个表格就是 Codex 搜索应用所使用的标签数据。
        ''' </summary>
        <Extension>
        Public Function LoadProcessedLabels(path$,
                                           Optional encoding As Encoding = Nothing,
                                           Optional mute As Boolean = False) As List(Of ProcessedLabels)

            Return path.LoadCsv(Of ProcessedLabels)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamProcessedLabels(path$,
                                              Optional encoding As Encoding = Nothing,
                                              Optional parallel As Boolean = False) As IEnumerable(Of ProcessedLabels)

            Return path.OpenDataStream(Of ProcessedLabels)(encoding, parallel)
        End Function

#End Region

#Region "Connections (Filtered)"

        ''' <summary>
        ''' 加载 ``connections_princeton.csv`` 表格 (5,342,446 行)：细胞连接矩阵，
        ''' 少于 5 个突触的连接已经被过滤掉了。
        ''' </summary>
        ''' <remarks>
        ''' 推荐使用 <see cref="StreamConnections"/> 以流式的方式读取这个表格。
        ''' </remarks>
        <Extension>
        Public Function LoadConnections(path$,
                                        Optional encoding As Encoding = Nothing,
                                        Optional mute As Boolean = False) As List(Of Connections)

            Return path.LoadCsv(Of Connections)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamConnections(path$,
                                          Optional encoding As Encoding = Nothing,
                                          Optional parallel As Boolean = False) As IEnumerable(Of Connections)

            Return path.OpenDataStream(Of Connections)(encoding, parallel)
        End Function

#End Region

#Region "Connections (Unfiltered)"

        ''' <summary>
        ''' 加载 ``connections_princeton_no_threshold.csv`` 表格 (22,285,323 行)：
        ''' 没有过滤掉少于 5 个突触的连接。
        ''' </summary>
        ''' <remarks>
        ''' 推荐使用 <see cref="StreamConnectionsNoThreshold"/> 以流式的方式读取这个表格。
        ''' </remarks>
        <Extension>
        Public Function LoadConnectionsNoThreshold(path$,
                                                   Optional encoding As Encoding = Nothing,
                                                   Optional mute As Boolean = False) As List(Of ConnectionsNoThreshold)

            Return path.LoadCsv(Of ConnectionsNoThreshold)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamConnectionsNoThreshold(path$,
                                                     Optional encoding As Encoding = Nothing,
                                                     Optional parallel As Boolean = False) As IEnumerable(Of ConnectionsNoThreshold)

            Return path.OpenDataStream(Of ConnectionsNoThreshold)(encoding, parallel)
        End Function

#End Region

#Region "Connections Predicted With Buhmann Et. Al."

        ''' <summary>
        ''' 加载 ``connections_buhmann_no_threshold.csv`` 表格 (16,847,997 行)：
        ''' 旧版本的连接矩阵数据 (使用 Buhmann 等人的突触预测方法)。
        ''' </summary>
        ''' <remarks>
        ''' 推荐使用 <see cref="StreamConnectionsBuhmannNoThreshold"/> 以流式的方式读取这个表格。
        ''' </remarks>
        <Extension>
        Public Function LoadConnectionsBuhmannNoThreshold(path$,
                                                          Optional encoding As Encoding = Nothing,
                                                          Optional mute As Boolean = False) As List(Of ConnectionsBuhmannNoThreshold)

            Return path.LoadCsv(Of ConnectionsBuhmannNoThreshold)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamConnectionsBuhmannNoThreshold(path$,
                                                            Optional encoding As Encoding = Nothing,
                                                            Optional parallel As Boolean = False) As IEnumerable(Of ConnectionsBuhmannNoThreshold)

            Return path.OpenDataStream(Of ConnectionsBuhmannNoThreshold)(encoding, parallel)
        End Function

#End Region

#Region "Connectivity Tags"

        ''' <summary>
        ''' 加载 ``connectivity_tags.csv`` 表格 (134,437 行)：基于网络分析计算出来的神经元的
        ''' 连接特征标签，同一个细胞可以有零个或者多个标签 (以逗号分隔)。
        ''' </summary>
        <Extension>
        Public Function LoadConnectivityTags(path$,
                                             Optional encoding As Encoding = Nothing,
                                             Optional mute As Boolean = False) As List(Of ConnectivityTags)

            Return path.LoadCsv(Of ConnectivityTags)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamConnectivityTags(path$,
                                               Optional encoding As Encoding = Nothing,
                                               Optional parallel As Boolean = False) As IEnumerable(Of ConnectivityTags)

            Return path.OpenDataStream(Of ConnectivityTags)(encoding, parallel)
        End Function

#End Region

#Region "Marked Neuron Coordinates"

        ''' <summary>
        ''' 加载 ``coordinates.csv`` 表格 (238,909 行)：人工校对过程中所标记的细胞位置坐标
        ''' 以及对应的 supervoxel 编号。一个细胞可以有零个或者多个标记位置。
        ''' </summary>
        <Extension>
        Public Function LoadCoordinates(path$,
                                        Optional encoding As Encoding = Nothing,
                                        Optional mute As Boolean = False) As List(Of Coordinates)

            Return path.LoadCsv(Of Coordinates)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamCoordinates(path$,
                                          Optional encoding As Encoding = Nothing,
                                          Optional parallel As Boolean = False) As IEnumerable(Of Coordinates)

            Return path.OpenDataStream(Of Coordinates)(encoding, parallel)
        End Function

#End Region

#Region "Synapse Table"

        ''' <summary>
        ''' 加载 ``fafb_v783_princeton_synapse_table.csv`` 表格 (80,215,790 行)：
        ''' 已校对细胞之间的单个突触的尺寸以及坐标。
        ''' </summary>
        ''' <remarks>
        ''' 这个表格的行数非常大，必须使用 <see cref="StreamSynapseTable"/> 以流式的方式读取。
        ''' </remarks>
        <Extension>
        Public Function LoadSynapseTable(path$,
                                         Optional encoding As Encoding = Nothing,
                                         Optional mute As Boolean = False) As List(Of SynapseTable)

            Return path.LoadCsv(Of SynapseTable)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamSynapseTable(path$,
                                           Optional encoding As Encoding = Nothing,
                                           Optional parallel As Boolean = False) As IEnumerable(Of SynapseTable)

            Return path.OpenDataStream(Of SynapseTable)(encoding, parallel)
        End Function

#End Region

#Region "Synapse Coordinates"

        ''' <summary>
        ''' 加载 ``synapse_coordinates.csv`` 表格 (34,156,320 行)：
        ''' 旧版本的单个突触坐标数据。
        ''' </summary>
        ''' <remarks>
        ''' 这个表格的行数非常大，推荐使用 <see cref="StreamSynapseCoordinates"/> 以流式的方式读取。
        ''' </remarks>
        <Extension>
        Public Function LoadSynapseCoordinates(path$,
                                               Optional encoding As Encoding = Nothing,
                                               Optional mute As Boolean = False) As List(Of SynapseCoordinates)

            Return path.LoadCsv(Of SynapseCoordinates)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamSynapseCoordinates(path$,
                                                 Optional encoding As Encoding = Nothing,
                                                 Optional parallel As Boolean = False) As IEnumerable(Of SynapseCoordinates)

            Return path.OpenDataStream(Of SynapseCoordinates)(encoding, parallel)
        End Function

#End Region

#Region "Synapse Attachment Rates"

        ''' <summary>
        ''' 加载 ``synapse_attachment_rates.csv`` 表格：按照 neuropil 统计的 pre/post 突触
        ''' 附着到已校对细胞上的比例。
        ''' </summary>
        <Extension>
        Public Function LoadSynapseAttachmentRates(path$,
                                                   Optional encoding As Encoding = Nothing,
                                                   Optional mute As Boolean = False) As List(Of SynapseAttachmentRates)

            Return path.LoadCsv(Of SynapseAttachmentRates)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamSynapseAttachmentRates(path$,
                                                     Optional encoding As Encoding = Nothing,
                                                     Optional parallel As Boolean = False) As IEnumerable(Of SynapseAttachmentRates)

            Return path.OpenDataStream(Of SynapseAttachmentRates)(encoding, parallel)
        End Function

#End Region

#Region "Per Neuropil Connection And Synapse Counts"

        ''' <summary>
        ''' 加载 ``neuropil_synapse_table.csv`` 表格 (134,181 行, 321 列)：
        ''' 按照 neuropil 统计的输入/输出突触数量以及输入/输出 partner 细胞的数量。
        ''' </summary>
        ''' <remarks>
        ''' 每一行包含 320 个计数值 (Double)，全量加载时的内存占用比较大，
        ''' 推荐使用 <see cref="StreamNeuropilSynapseTable"/> 以流式的方式读取这个表格。
        ''' </remarks>
        <Extension>
        Public Function LoadNeuropilSynapseTable(path$,
                                                 Optional encoding As Encoding = Nothing,
                                                 Optional mute As Boolean = False) As List(Of NeuropilSynapseTable)

            Return path.LoadCsv(Of NeuropilSynapseTable)(encoding:=encoding, mute:=mute)
        End Function

        <Extension>
        Public Function StreamNeuropilSynapseTable(path$,
                                                   Optional encoding As Encoding = Nothing,
                                                   Optional parallel As Boolean = False) As IEnumerable(Of NeuropilSynapseTable)

            Return path.OpenDataStream(Of NeuropilSynapseTable)(encoding, parallel)
        End Function

#End Region

    End Module
End Namespace
