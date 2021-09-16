using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SystemUI;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.AnalysisTools;
using ESRI.ArcGIS.NetworkAnalysis;
using ESRI.ArcGIS.Analyst3D;

namespace MyGISystem
{
    //功能操作类型枚举
    public enum functionOperationType
    {
        //无操作
        None,
        //放大视图
        zoomIn,
        //缩小视图
        zoomOut,
        //平移视图
        Pan,
        //按点查询
        spatialQueryByPoint,
        //按折线查询
        spatialQueryByLine,
        //按矩形查询
        spatialQueryByRectangle,
        //按圆查询
        spatialQueryByCircle,
        //按多边形查询
        spatialQueryByPolygon,
        //地图编辑
        Edit,
        //网络分析
        networkAnalysis,
    }

    //地图编辑方式类型枚举
    public enum editOperationType
    {
        //无操作
        None,
        //添加对象
        Create,
        //移动对象
        Move,
        //删除对象
        Delete,
    }

    public partial class Form1 : Form
    {
        //当前的功能操作类型
        functionOperationType m_OperationType = functionOperationType.None;
        //当前查询的图层
        int layerIndex = 0;
        //当前叠置的图层
        int intersectLayerIndex = 0;

        #region 地图编辑功能的成员变量
        editOperationType m_editOperationType = editOperationType.None; //当前的地图编辑方式
        private bool isEditing;                                         //是否处于编辑状态
        private bool haveEditing;                                       //是否已经执行过编辑
        private IFeatureLayer currentEditingLayer;                      //当前编辑图层
        private IWorkspaceEdit currentEditingWorkspace;                 //当前编辑工作空间
        private IMap currentEditingMap;                                 //当前编辑地图
        private IDisplayFeedback editingDisplayFeedback;                //用于鼠标与控件进行可视化交互
        private IFeature editingMovingFeature;                          //移动的要素
        #endregion

        #region 网络分析功能的成员变量
        //几何网络
        private IGeometricNetwork mGeometricNetwork;
        //给定点的集合
        private IPointCollection mPointCollection;
        //获取给定点最近的Network元素
        private IPointToEID mPointToEID;
        //返回结果变量
        private IEnumNetEID mEnumNetEID_Junctions;
        private IEnumNetEID mEnumNetEID_Edges;
        private double mdblPathCost;
        #endregion

        #region 三维分析功能的成员变量
        //是否处于点查询状态
        private bool is3DPointQuery;
        #endregion


        public Form1()
        {
            InitializeComponent();
            //构建三维控件中的鼠标滚轮函数
            this.MouseWheel += new System.Windows.Forms.MouseEventHandler(axSceneControl1_wheelZoom);
        }

        /****** 框架初始化加载事件 ******/
        private void Form1_Load(object sender, EventArgs e)
        {
            //设置axTOCControl1的伙伴控件为主地图控件
            this.axTOCControl1.SetBuddyControl(axMapControl1.Object);

            //将操作图层和叠置图层下拉框中的选项清空
            queryIndexComboBox1.Items.Clear();
            intersectIndexBox1.Items.Clear();
            IMap pMap = axMapControl1.Map;
            //清空鹰眼地图中的图层
            axMapControl2.Map.ClearLayers();

            //基于主地图控件中的所有图层进行循环
            for (int i = pMap.LayerCount - 1; i >= 0; i--)
            {
                //向鹰眼地图中逐个添加图层
                axMapControl2.Map.AddLayer(pMap.get_Layer(i));
            }
            //基于主地图控件中的所有图层循环向查询图层下拉框中添加选项
            for (int i = 0; i < pMap.LayerCount; i++)
            {
                queryIndexComboBox1.Items.Add(pMap.get_Layer(i).Name);
                queryIndexComboBox1.SelectedIndex = 0;
                intersectIndexBox1.Items.Add(pMap.get_Layer(i).Name);
                intersectIndexBox1.SelectedIndex = 0;
            }

            //将地图编辑的选择操作类型、保存编辑结果、结束编辑菜单设置为不可选
            this.selectEditingOperationToolStripMenuItem.Enabled = false;
            this.saveEditingResultToolStripMenuItem.Enabled = false;
            this.finishEditingToolStripMenuItem.Enabled = false;

            //将三维点查询状态处于关闭状态
            is3DPointQuery = false;

        }

        /****** 主地图控件的鼠标按下的响应事件 ******/
        private void axMapControl1_OnMouseDown(object sender, ESRI.ArcGIS.Controls.IMapControlEvents2_OnMouseDownEvent e)
        {
            //放大视图操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            if (m_OperationType == functionOperationType.zoomIn)
            {
                //在主地图中绘制一个矩形
                IEnvelope pEnvTrackRectangle = axMapControl1.TrackRectangle();
                //当前显示的地图矩形范围
                IEnvelope pCurEnv = axMapControl1.Extent;
                //确定视图放大的比例
                double widthRatio = pEnvTrackRectangle.Width / pCurEnv.Width;
                double heightRatio = pEnvTrackRectangle.Height / pCurEnv.Height;
                double averageRatio = (widthRatio + heightRatio) / 2;
                //得到按比例放大后的显示范围
                pCurEnv.Expand(widthRatio, heightRatio, true);
                //将主地图控件的显示范围设置为放大后的显示范围
                axMapControl1.Extent = pCurEnv;

                //将绘制的矩形的中心作为放大后的视图中心
                IPoint pNewCnt = new ESRI.ArcGIS.Geometry.Point();
                pNewCnt.X = (pEnvTrackRectangle.XMin + pEnvTrackRectangle.XMax) / 2;
                pNewCnt.Y = (pEnvTrackRectangle.YMin + pEnvTrackRectangle.YMax) / 2;
                axMapControl1.CenterAt(pNewCnt);
            }

            //缩小视图操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.zoomOut)
            {
                //在主地图中绘制一个矩形
                IEnvelope pEnvTrackRectangle = axMapControl1.TrackRectangle();
                //当前显示的地图矩形范围
                IEnvelope pCurEnv = axMapControl1.Extent;
                //确定视图缩小的比例
                double widthRatio = pCurEnv.Width / pEnvTrackRectangle.Width;
                double heightRatio = pCurEnv.Height / pEnvTrackRectangle.Height;
                double averageRatio = (widthRatio + heightRatio) / 2;
                //得到按比例缩小后的显示范围
                pCurEnv.Expand(widthRatio, heightRatio, true);
                //将主地图控件的显示范围设置为缩小后的显示范围
                axMapControl1.Extent = pCurEnv;

                //将绘制的矩形的中心作为缩小后的视图中心
                IPoint pNewCnt = new ESRI.ArcGIS.Geometry.Point();
                pNewCnt.X = (pEnvTrackRectangle.XMin + pEnvTrackRectangle.XMax) / 2;
                pNewCnt.Y = (pEnvTrackRectangle.YMin + pEnvTrackRectangle.YMax) / 2;
                axMapControl1.CenterAt(pNewCnt);

            }

            //视图平移操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.Pan)
            {
                //直接调用接口函数
                axMapControl1.Pan();
            }

            //按矩形查询操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.spatialQueryByRectangle)
            {
                //在主地图中绘制一个矩形
                IEnvelope pTrackGeo = axMapControl1.TrackRectangle();
                //设置查询图层
                IFeatureLayer pFeatureLayer = axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
                //设置空间查询的空间关系为广义相交
                esriSpatialRelEnum queryType = esriSpatialRelEnum.esriSpatialRelIntersects;
                //执行空间查询
                geoQuery(pFeatureLayer, pTrackGeo, queryType);
            }

            //按圆查询操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.spatialQueryByCircle)
            {
                //在主地图中绘制一个圆
                IGeometry pTrackGeo = axMapControl1.TrackCircle();
                //设置查询图层
                IFeatureLayer pFeatureLayer = axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
                //设置空间查询的空间关系为广义相交
                esriSpatialRelEnum queryType = esriSpatialRelEnum.esriSpatialRelIntersects;
                //执行空间查询
                geoQuery(pFeatureLayer, pTrackGeo, queryType);
            }

            //按多边形查询操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.spatialQueryByPolygon)
            {
                //在主地图中绘制一个多边形
                IGeometry pTrackGeo = axMapControl1.TrackPolygon();
                //设置查询图层
                IFeatureLayer pFeatureLayer = axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
                //设置空间查询的空间关系为广义相交
                esriSpatialRelEnum queryType = esriSpatialRelEnum.esriSpatialRelIntersects;
                //执行空间查询
                geoQuery(pFeatureLayer, pTrackGeo, queryType);
            }

            //按点查询操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.spatialQueryByPoint)
            {
                //获取视图范围
                IActiveView pActiveView = this.axMapControl1.ActiveView;
                //获取鼠标点击屏幕坐标
                IPoint pPoint = pActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);

                //将IPoint对象转换为IGeometry类型
                IGeometry pTrackGeo = pPoint as IGeometry;

                //设置查询图层
                IFeatureLayer pFeatureLayer = axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
                //设置空间查询的空间关系为广义相交
                esriSpatialRelEnum queryType = esriSpatialRelEnum.esriSpatialRelIntersects;
                //执行空间查询
                geoQuery(pFeatureLayer, pTrackGeo, queryType);
            }

            //按折线查询操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.spatialQueryByLine)
            {
                //在主地图中绘制一条折线
                IGeometry pTrackGeo = axMapControl1.TrackLine();
                //设置查询图层
                IFeatureLayer pFeatureLayer = axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
                //设置空间查询的空间关系为广义相交
                esriSpatialRelEnum queryType = esriSpatialRelEnum.esriSpatialRelIntersects;
                //执行空间查询
                geoQuery(pFeatureLayer, pTrackGeo, queryType);
            }

            //地图编辑-添加要素操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.Edit && isEditing == true && m_editOperationType == editOperationType.Create)
            {
                //鼠标点击位置
                //获取视图范围
                IActiveView pActiveView = this.axMapControl1.ActiveView;
                //获取鼠标点击屏幕坐标
                IPoint pPoint = pActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);

                INewLineFeedback pNewLineFeedback;
                INewPolygonFeedback pNewPolygonFeedback;
                //针对线和多边形，判断交互状态，第一次时要初始化，再次点击则直接添加节点
                if (editingDisplayFeedback == null)
                {
                    //根据图层类型创建不同要素
                    switch (currentEditingLayer.FeatureClass.ShapeType)
                    {
                        case esriGeometryType.esriGeometryPoint:
                            //添加点要素
                            AddFeature(pPoint);
                            break;
                        case esriGeometryType.esriGeometryPolyline:
                            editingDisplayFeedback = new NewLineFeedback();
                            //获取当前屏幕显示
                            editingDisplayFeedback.Display = ((IActiveView)this.currentEditingMap).ScreenDisplay;
                            pNewLineFeedback = editingDisplayFeedback as INewLineFeedback;
                            //开始追踪
                            pNewLineFeedback.Start(pPoint);
                            break;
                        case esriGeometryType.esriGeometryPolygon:
                            editingDisplayFeedback = new NewPolygonFeedback();
                            editingDisplayFeedback.Display = ((IActiveView)this.currentEditingMap).ScreenDisplay;
                            pNewPolygonFeedback = editingDisplayFeedback as INewPolygonFeedback;
                            //开始追踪
                            pNewPolygonFeedback.Start(pPoint);
                            break;
                    }

                }
                else //第一次之后的点击则添加节点
                {
                    if (editingDisplayFeedback is INewLineFeedback)
                    {
                        pNewLineFeedback = editingDisplayFeedback as INewLineFeedback;
                        pNewLineFeedback.AddPoint(pPoint);
                    }
                    else if (editingDisplayFeedback is INewPolygonFeedback)
                    {
                        pNewPolygonFeedback = editingDisplayFeedback as INewPolygonFeedback;
                        pNewPolygonFeedback.AddPoint(pPoint);
                    }
                }
            }

            //地图编辑-移动要素操作对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.Edit && isEditing == true && m_editOperationType == editOperationType.Move)
            {
                //清除地图选择集
                currentEditingMap.ClearSelection();

                IActiveView pActiveView = currentEditingMap as IActiveView;
                //获取鼠标点击位置
                IPoint pPoint = pActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);
                //获取点击到的要素
                editingMovingFeature = SelectFeature(pPoint);
                if (editingMovingFeature == null)
                    return;
                //获取要素形状
                IGeometry pGeometry = editingMovingFeature.Shape;

                IMovePointFeedback pMovePointFeedback;
                IMoveLineFeedback pMoveLineFeedback;
                IMovePolygonFeedback pMovePolygonFeedback;
                //根据要素类型定义移动方式
                switch (pGeometry.GeometryType)
                {
                    case esriGeometryType.esriGeometryPoint:
                        editingDisplayFeedback = new MovePointFeedback();
                        //获取屏幕显示
                        editingDisplayFeedback.Display = pActiveView.ScreenDisplay;
                        //开始追踪
                        pMovePointFeedback = editingDisplayFeedback as IMovePointFeedback;
                        pMovePointFeedback.Start((IPoint)pGeometry, pPoint);
                        break;
                    case esriGeometryType.esriGeometryPolyline:
                        editingDisplayFeedback = new MoveLineFeedback();
                        editingDisplayFeedback.Display = pActiveView.ScreenDisplay;
                        //开始追踪
                        pMoveLineFeedback = editingDisplayFeedback as IMoveLineFeedback;
                        pMoveLineFeedback.Start((IPolyline)pGeometry, pPoint);
                        break;
                    case esriGeometryType.esriGeometryPolygon:
                        editingDisplayFeedback = new MovePolygonFeedback();
                        editingDisplayFeedback.Display = pActiveView.ScreenDisplay;
                        //开始追踪
                        pMovePolygonFeedback = editingDisplayFeedback as IMovePolygonFeedback;
                        pMovePolygonFeedback.Start((IPolygon)pGeometry, pPoint);
                        break;
                }
            }

            //网络分析对应在主地图控件中的鼠标保持点击状态时的响应事件
            else if (m_OperationType == functionOperationType.networkAnalysis)
            {
                //记录鼠标点击的点
                IPoint pNewPoint = new PointClass();
                pNewPoint.PutCoords(e.mapX, e.mapY);

                AddFeature(pNewPoint);

                if (mPointCollection == null) { mPointCollection = new Multipoint(); }
                //添加点，before和after标记添加点的索引，这里不定义
                object before = Type.Missing;
                object after = Type.Missing;
                mPointCollection.AddPoint(pNewPoint, ref before, ref after);
            }

        }

        /****** 主地图控件的鼠标移动的响应事件 ******/
        private void axMapControl1_OnMouseMove(object sender, IMapControlEvents2_OnMouseMoveEvent e)
        {

            // 显示当前比例尺
            this.scaleToolStripStatusLabel.Text = " 比例尺 1:" + ((long)this.axMapControl1.MapScale).ToString();
            // 显示当前鼠标所在的坐标
            this.coordinateToolStripStatusLabel.Text = " 当前坐标 X = " + e.mapX.ToString() + " Y = " + e.mapY.ToString() + " " + this.axMapControl1.MapUnits;

            //地图编辑操作对应在主地图控件中的鼠标移动的响应事件
            if (m_OperationType == functionOperationType.Edit && isEditing == true)
            {
                if (editingDisplayFeedback == null)
                    return;
                //获取鼠标移动点位，并移动至当前点位
                //获取视图范围
                IActiveView pActiveView = this.axMapControl1.ActiveView;
                //获取鼠标点击屏幕坐标
                IPoint pPoint = pActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);
                editingDisplayFeedback.MoveTo(pPoint);
            }
        }

        /****** 主地图控件的鼠标抬起的响应事件 ******/
        private void axMapControl1_OnMouseUp(object sender, IMapControlEvents2_OnMouseUpEvent e)
        {
            //判断是否鼠标左键
            if (e.button != 1)
                return;

            //地图编辑操作对应在主地图控件中的鼠标抬起的响应事件
            if (m_OperationType == functionOperationType.Edit && isEditing == true)
            {
                switch (m_editOperationType)
                {
                    //地图编辑-添加要素操作对应在主地图控件中的鼠标抬起的响应事件
                    case editOperationType.Create:
                        break;
                    //地图编辑-移动要素操作对应在主地图控件中的鼠标抬起的响应事件
                    case editOperationType.Move:
                        {
                            if (editingDisplayFeedback == null)
                                return;
                            //获取点位
                            IActiveView pActiveView = currentEditingMap as IActiveView;
                            //获取鼠标点击屏幕坐标
                            IPoint pPoint = pActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);

                            IMovePointFeedback pMovePointFeedback;
                            IMoveLineFeedback pMoveLineFeedback;
                            IMovePolygonFeedback pMovePolygonFeedback;
                            IGeometry pGeometry;
                            //根据移动要素类型选择移动方式
                            if (editingDisplayFeedback is IMovePointFeedback)
                            {
                                pMovePointFeedback = editingDisplayFeedback as IMovePointFeedback;
                                //结束追踪
                                pGeometry = pMovePointFeedback.Stop();
                                //更新要素
                                UpdateFeature(editingMovingFeature, pGeometry);
                            }
                            else if (editingDisplayFeedback is IMoveLineFeedback)
                            {
                                pMoveLineFeedback = editingDisplayFeedback as IMoveLineFeedback;
                                //结束追踪
                                pGeometry = pMoveLineFeedback.Stop();
                                //更新要素
                                UpdateFeature(editingMovingFeature, pGeometry);
                            }
                            else if (editingDisplayFeedback is IMovePolygonFeedback)
                            {
                                pMovePolygonFeedback = editingDisplayFeedback as IMovePolygonFeedback;
                                pGeometry = pMovePolygonFeedback.Stop();
                                UpdateFeature(editingMovingFeature, pGeometry);
                            }
                            editingDisplayFeedback = null;
                            pActiveView.Refresh();
                            break;
                        }
                }
            }
        }

        /****** 主地图控件的鼠标双击的响应事件 ******/
        private void axMapControl1_OnDoubleClick(object sender, IMapControlEvents2_OnDoubleClickEvent e)
        {
            //判断是否鼠标左键
            if (e.button != 1)
                return;

            //地图编辑操作对应在主地图控件中的鼠标双击的响应事件
            if (m_OperationType == functionOperationType.Edit && isEditing == true)
            {
                switch (m_editOperationType)
                {
                    case editOperationType.Create:
                        {
                            if (editingDisplayFeedback == null)
                                return;
                            IGeometry pGeometry = null;
                            //获取视图范围
                            IActiveView pActiveView = this.axMapControl1.ActiveView;
                            //获取鼠标点击屏幕坐标
                            IPoint pPoint = pActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);

                            INewLineFeedback pNewLineFeedback;
                            INewPolygonFeedback pNewPolygonFeedback;
                            IPointCollection pPointCollection;

                            if (editingDisplayFeedback is INewLineFeedback)
                            {
                                pNewLineFeedback = editingDisplayFeedback as INewLineFeedback;
                                //添加点击点
                                pNewLineFeedback.AddPoint(pPoint);
                                //结束Feedback
                                IPolyline pPolyline = pNewLineFeedback.Stop();
                                pPointCollection = pPolyline as IPointCollection;
                                //至少两点时才创建线要素
                                if (pPointCollection.PointCount < 2)
                                    MessageBox.Show("至少需要两点才能建立线要素！", "提示");
                                else
                                    pGeometry = pPolyline as IGeometry;
                            }
                            else if (editingDisplayFeedback is INewPolygonFeedback)
                            {
                                pNewPolygonFeedback = editingDisplayFeedback as INewPolygonFeedback;
                                //添加点击点
                                pNewPolygonFeedback.AddPoint(pPoint);
                                //结束Feedback
                                IPolygon pPolygon = pNewPolygonFeedback.Stop();
                                pPointCollection = pPolygon as IPointCollection;
                                //至少三点才能创建面要素
                                if (pPointCollection.PointCount < 3)
                                    MessageBox.Show("至少需要三点才能建立面要素！", "提示");
                                else
                                    pGeometry = pPolygon as IGeometry;
                            }
                            editingDisplayFeedback.Display = ((IActiveView)this.currentEditingMap).ScreenDisplay;
                            //不为空时添加
                            if (pGeometry != null)
                            {
                                AddFeature(pGeometry);
                                //创建完成将DisplayFeedback置为空
                                editingDisplayFeedback = null;
                            }

                            break;
                        }
                    case editOperationType.Move:
                        break;
                }
            }

            //网络分析操作对应在主地图控件中的鼠标双击的响应事件
            else if (m_OperationType == functionOperationType.networkAnalysis)
            {
                try
                {
                    //路径计算
                    //注意权重名称与设置保持一致，在这里，权重名称设为"LENGTH"
                    SolvePath("LENGTH");
                    //路径转换为几何要素
                    IPolyline pPolyLineResult = PathToPolyLine();
                    //获取屏幕显示
                    IActiveView pActiveView = this.axMapControl1.ActiveView;
                    IScreenDisplay pScreenDisplay = pActiveView.ScreenDisplay;
                    //设置显示符号
                    ILineSymbol pLineSymbol = new CartographicLineSymbol();
                    IRgbColor pColor = new RgbColor();
                    pColor.Red = 255;
                    pColor.Green = 0;
                    pColor.Blue = 0;
                    //设置线宽
                    pLineSymbol.Width = 4;
                    //设置颜色
                    pLineSymbol.Color = pColor as IColor;
                    //绘制线型符号
                    pScreenDisplay.StartDrawing(0, 0);
                    pScreenDisplay.SetSymbol((ISymbol)pLineSymbol);
                    pScreenDisplay.DrawPolyline(pPolyLineResult);
                    pScreenDisplay.FinishDrawing();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("路径分析出现错误:" + "\r\n" + ex.Message);
                }
                //点集设为空
                mPointCollection = null;
            }


        }

        /****** 主地图控件的地图替换时的响应事件 ******/
        private void axMapControl1_OnMapReplaced(object sender, IMapControlEvents2_OnMapReplacedEvent e)
        {
            //将查询图层下拉框中的选项清空
            queryIndexComboBox1.Items.Clear();
            IMap pMap = axMapControl1.Map;
            //清空鹰眼地图中的图层
            axMapControl2.Map.ClearLayers();

            //基于主地图控件中的所有图层进行循环
            for (int i = pMap.LayerCount - 1; i >= 0; i--)
            {
                //向鹰眼地图中逐个添加图层
                axMapControl2.Map.AddLayer(pMap.get_Layer(i));
            }
            //基于主地图控件中的所有图层循环向查询图层下拉框中添加选项
            for (int i = 0; i < pMap.LayerCount; i++)
            {
                queryIndexComboBox1.Items.Add(pMap.get_Layer(i).Name);
                queryIndexComboBox1.SelectedIndex = 0;
                intersectIndexBox1.Items.Add(pMap.get_Layer(i).Name);
                intersectIndexBox1.SelectedIndex = 0;
            }
        }

        /****** 主地图控件的地图范围发生改变时的响应事件 ******/
        private void axMapControl1_OnExtentUpdated(object sender, IMapControlEvents2_OnExtentUpdatedEvent e)
        {
            //新建一个视图范围
            IEnvelope pEnv;
            pEnv = e.newEnvelope as IEnvelope;
            //图形容器
            IGraphicsContainer pGraphicsContainer;
            IActiveView pActiveView;
            pGraphicsContainer = axMapControl2.Map as IGraphicsContainer;
            pActiveView = pGraphicsContainer as IActiveView;
            //在绘制前，DeleteAllElements清除axMapControl2中的所有图形元素
            pGraphicsContainer.DeleteAllElements();

            //获取矩形坐标
            RectangleElement pRectangleEle = new RectangleElement();
            IElement pElement = pRectangleEle as IElement;
            pElement.Geometry = pEnv;

            //设置矩形线框的颜色
            IRgbColor pColor;
            pColor = new RgbColor();
            pColor.Red = 255;
            pColor.Green = 0;
            pColor.Blue = 0;
            pColor.Transparency = 255;

            //产生一个矩形线框对象
            ILineSymbol pOutline;
            pOutline = new SimpleLineSymbol();
            pOutline.Width = 1;
            pOutline.Color = pColor;

            //设置填充符号的颜色属性
            pColor = new RgbColor();
            pColor.RGB = 255;
            pColor.Transparency = 0;

            //设置填充符号的属性
            IFillSymbol pFillSymbol;
            pFillSymbol = new SimpleFillSymbol();
            pFillSymbol.Color = pColor;
            pFillSymbol.Outline = pOutline;

            //构建矩形元素
            IFillShapeElement pFillshapeEle;
            pFillshapeEle = pElement as IFillShapeElement;
            pFillshapeEle.Symbol = pFillSymbol;
            pElement = pFillshapeEle as IElement;
            pGraphicsContainer.AddElement(pElement, 0);
            pActiveView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }

        /****** 加载地图文档的菜单鼠标点击响应事件 ******/
        private void loadMxdFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //打开文件资源管理器选择加载的文件
            OpenFileDialog mxdDialog = new OpenFileDialog();
            mxdDialog.Title = "打开地图文档";
            mxdDialog.Filter = "地图文档(*.mxd)|*.mxd";
            if (mxdDialog.ShowDialog() != DialogResult.OK) { return; }
            string strMxdFile = mxdDialog.FileName;

            //将地图文档加载到主地图控件中
            axMapControl1.LoadMxFile(strMxdFile);
        }

        /****** 加载矢量图层的菜单鼠标点击响应事件 ******/
        private void addShpFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //打开文件资源管理器选择加载的文件
            OpenFileDialog shpDialog = new OpenFileDialog();
            shpDialog.Title = "打开矢量图层文件";
            shpDialog.Filter = "矢量图层文件(*.shp)|*.shp";
            if (shpDialog.ShowDialog() != DialogResult.OK) { return; }
            string strShpFile = shpDialog.FileName;

            //AddShapeFile函数需要提供两个参数，分别为路径和姓名，使用IO获取*shp文件对应的路径和姓名
            string shpFilePath = System.IO.Path.GetDirectoryName(strShpFile);
            string shpFileName = System.IO.Path.GetFileName(strShpFile);
            //将矢量图层加载到主地图控件中
            axMapControl1.AddShapeFile(shpFilePath, shpFileName);
        }

        /****** 加载图层文件的菜单鼠标点击响应事件 ******/
        private void addLyrFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //打开文件资源管理器选择加载的文件
            OpenFileDialog lyrDialog = new OpenFileDialog();
            lyrDialog.Title = "打开图层文件";
            lyrDialog.Filter = "图层文件(*.lyr)|*.lyr";
            if (lyrDialog.ShowDialog() != DialogResult.OK) { return; }
            string strLyrFile = lyrDialog.FileName;

            //将图层文件加载到主地图控件中
            axMapControl1.AddLayerFromFile(strLyrFile);
        }

        /****** 加载个人地理数据库的菜单鼠标点击响应事件 ******/
        private void loadMdbFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //获得mdb数据库文件的路径
            OpenFileDialog gdbDialog = new OpenFileDialog();
            gdbDialog.Title = "打开个人地理数据库";
            gdbDialog.Filter = "ESRI Personal Geodatabase(*mdb)|*.mdb";
            if (gdbDialog.ShowDialog() != DialogResult.OK) { return; }
            string strGdbFile = gdbDialog.FileName;

            //打开数据库
            IWorkspaceFactory pWskF = new AccessWorkspaceFactory();
            IWorkspace pPGDB = pWskF.OpenFromFile(strGdbFile, 0);
            //获取数据库中的矢量数据集
            IEnumDataset pEnumDT = pPGDB.get_Datasets(esriDatasetType.esriDTAny);
            pEnumDT.Reset();
            IDataset pDT = pEnumDT.Next();
            while (pDT != null)
            {
                //pDT是要素数据集
                if (pDT is IFeatureDataset)
                {
                    IEnumDataset pEnumFeatureDT = pDT.Subsets;
                    pEnumFeatureDT.Reset();
                    IDataset pFCDT = pEnumFeatureDT.Next();
                    while (pFCDT != null)
                    {
                        //判断pFGDT是不是FeatureClass
                        if (pFCDT is IFeatureClass)
                        {
                            IFeatureClass pFC = (IFeatureClass)pFCDT;
                            //加到地图控件中
                            IFeatureLayer PFlyr = new FeatureLayer();
                            PFlyr.FeatureClass = pFC;
                            axMapControl1.AddLayer(PFlyr);
                        }
                        pFCDT = pEnumFeatureDT.Next();
                    }
                }
                //pDT是要素类

                else if (pDT is IFeatureClass)
                {
                    IFeatureClass pFC = (IFeatureClass)pDT;
                    //加到地图控件中
                    IFeatureLayer PFlyr = new FeatureLayer();
                    PFlyr.FeatureClass = pFC;
                    axMapControl1.AddLayer(PFlyr);
                }
                pDT = pEnumDT.Next();
            }
        }

        /****** 保存为地图文档的菜单鼠标点击响应事件 ******/
        private void saveAsMxdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //打开文件资源管理器选择另存为的文件
            SaveFileDialog mxdSaveDialog = new SaveFileDialog();
            mxdSaveDialog.Title = "保存为地图文档（*mxd）";
            mxdSaveDialog.Filter = "地图文档（*mxd）|*mxd";

            //默认的保存初始路径和主地图控件的路径一致
            mxdSaveDialog.FileName = axMapControl1.DocumentFilename;
            if (mxdSaveDialog.ShowDialog() != DialogResult.OK) { return; }
            //获取到文件路径
            string saveFilePath = mxdSaveDialog.FileName;

            //使用EndsWith()函数判断文件名是否以".mxd"结尾
            if (!saveFilePath.EndsWith(".mxd"))
            {
                MessageBox.Show("请输入正确的保存的文件路径，以.mxd结尾！");
                return;
            }

            //IMxdContents是向地图文档传入或传出数据的接口
            IMxdContents pMxdContents = axMapControl1.Map as IMxdContents;
            //IMapDocument是控制读写地图文档组件MapDocument的接口
            IMapDocument m_MapDocument = new MapDocument();
            m_MapDocument.New(saveFilePath);
            m_MapDocument.ReplaceContents(pMxdContents);
            //保存为*.mxd地图文档
            m_MapDocument.Save(true, true);

        }

        /****** 清除所有图层的菜单鼠标点击响应事件 ******/
        private void clearLayersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axMapControl1.Map.ClearLayers();
            axMapControl2.Map.ClearLayers();
            axMapControl1.Refresh();
            axMapControl2.Refresh();
            axTOCControl1.Update();
        }

        /****** 视图放大的菜单鼠标点击响应事件 ******/
        private void zoomInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //功能操作符修改为zoomIn
            m_OperationType = functionOperationType.zoomIn;
            //鼠标形状修改成ESRI自带的放大符号
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerZoomIn;
        }

        /****** 视图放大的菜单鼠标点击响应事件 ******/
        private void zoomOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //功能操作符修改为zoomOut
            m_OperationType = functionOperationType.zoomOut;
            //鼠标形状修改成ESRI自带的缩小符号
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerZoomOut;
        }

        /****** 平移视图的菜单鼠标点击响应事件 ******/
        private void panToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //功能操作符修改为Pan
            m_OperationType = functionOperationType.Pan;
            //鼠标形状修改成ESRI自带的平移符号
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerPan;
        }

        /****** 视图缩放至全图的菜单鼠标点击响应事件 ******/
        private void fullExtentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //使用ICommand接口执行缩放至全图的功能，点击执行命令后，地图直接进行缩放，不需要和地图交互
            ICommand pCommand = new ControlsMapFullExtentCommand();
            pCommand.OnCreate(axMapControl1.Object);
            pCommand.OnClick();
        }

        /****** 中心放大视图的菜单鼠标点击响应事件 ******/
        private void zoomInFixedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //使用ICommand接口执行中心放大的功能，点击执行命令后，地图直接进行缩放，不需要和地图交互
            ICommand pCommand = new ControlsMapZoomInFixedCommand();
            pCommand.OnCreate(axMapControl1.Object);
            pCommand.OnClick();
        }

        /****** 中心缩小视图的菜单鼠标点击响应事件 ******/
        private void zoomOutFixedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //使用ICommand接口执行中心缩小的功能，点击执行命令后，地图直接进行缩放，不需要和地图交互
            ICommand pCommand = new ControlsMapZoomOutFixedCommand();
            pCommand.OnCreate(axMapControl1.Object);
            pCommand.OnClick();
        }

        /****** 恢复箭头的菜单鼠标点击响应事件 ******/
        private void arrowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //将当前所有操作清除，鼠标形状恢复成箭头
            axMapControl1.CurrentTool = null;
            m_OperationType = functionOperationType.None;
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrow;
        }

        /****** 属性查询的菜单鼠标点击响应事件 ******/
        private void attributeQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //获取数据
            ILayer pLayer = axMapControl1.get_Layer(layerIndex);
            IFeatureLayer pFeatureLyr = pLayer as IFeatureLayer;
            if (pFeatureLyr == null) { return; }//转换不成功返回

            //SQL语句
            IQueryFilter pQueryFilter = new QueryFilter();
            if (SQLTextBox1.Text == "")
            {
                MessageBox.Show("请输入SQL语句！");
                return;
            }
            pQueryFilter.WhereClause = SQLTextBox1.Text;

            //查询语句执行
            IFeatureCursor pFeatureCursor = pFeatureLyr.Search(pQueryFilter, true);

            //查询结果输出
            IFeature pFeature = pFeatureCursor.NextFeature();
            IPoint pNewCnt = new ESRI.ArcGIS.Geometry.Point();

            while (pFeature != null)
            {

                //闪烁3次，间隔300ms
                axMapControl1.FlashShape(pFeature.Shape, 3, 300, null);
                //将查询结果加到数据集中
                axMapControl1.Map.SelectFeature(pLayer, pFeature);
                pFeature = pFeatureCursor.NextFeature();
            }
            axMapControl1.Refresh();
        }

        /****** 对要素层进行几何查询的自定义函数geoQuery() ******/
        private void geoQuery(IFeatureLayer pFeatureLyr, IGeometry pGeometry, esriSpatialRelEnum queryType)
        {
            //数据:pFeatureLyr
            //空间查询的方式
            ISpatialFilter pSptialFilter = new SpatialFilter();
            pSptialFilter.Geometry = pGeometry;
            pSptialFilter.SpatialRel = queryType;

            //执行查询
            IFeatureCursor pFeatureCursor = pFeatureLyr.Search(pSptialFilter, true);

            //查询结果输出
            IFeature pFeature = pFeatureCursor.NextFeature();
            IPoint pNewCnt = new ESRI.ArcGIS.Geometry.Point();

            while (pFeature != null)
            {
                //闪烁3次，间隔300ms
                axMapControl1.FlashShape(pFeature.Shape, 3, 300, null);
                //将查询结果加到数据集中
                axMapControl1.Map.SelectFeature(pFeatureLyr, pFeature);
                pFeature = pFeatureCursor.NextFeature();
            }
            axMapControl1.Refresh();
        }

        /****** 空间查询-按矩形查询的菜单鼠标点击响应事件 ******/
        private void rectangleQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axMapControl1.Map.ClearSelection();
            m_OperationType = functionOperationType.spatialQueryByRectangle;
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrowQuestion;
        }

        /****** 空间查询-按圆查询的菜单鼠标点击响应事件 ******/
        private void circleQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axMapControl1.Map.ClearSelection();
            m_OperationType = functionOperationType.spatialQueryByCircle;
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrowQuestion;
        }

        /****** 空间查询-按多边形查询的菜单鼠标点击响应事件 ******/
        private void polygonQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axMapControl1.Map.ClearSelection();
            m_OperationType = functionOperationType.spatialQueryByPolygon;
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrowQuestion;
        }

        /****** 空间查询-按点查询的菜单鼠标点击响应事件 ******/
        private void pointQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axMapControl1.Map.ClearSelection();
            m_OperationType = functionOperationType.spatialQueryByPoint;
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrowQuestion;
        }

        /****** 空间查询-按折线查询的菜单鼠标点击响应事件 ******/
        private void lineQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axMapControl1.Map.ClearSelection();
            m_OperationType = functionOperationType.spatialQueryByLine;
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrowQuestion;
        }

        /****** 清除查询结果的菜单鼠标点击响应事件 ******/
        private void clearQueryResultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //获得axMapControl1的ActiveView
            //map和activeview实际上都是代表同一个东西，就是地图，activeview针对地图的显示，例如地图显示范围属性extent就在activeview
            IActiveView pActiveView = axMapControl1.ActiveView;
            //绑定图层
            IFeatureLayer pFeaturelayer = axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
            if (pActiveView == null || pFeaturelayer == null) { return; }

            //将原图层转换成IFeatureSelection，包含当前显示的所有查询结果
            IFeatureSelection featureSelection = pFeaturelayer as IFeatureSelection;
            //重绘地图中的部分内容，esriViewGeoSelection是函数参数，意思为刷新图层中的选中要素
            pActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
            //清除所有选择
            featureSelection.Clear();
        }

        /****** 改变操作查询图层下拉框的响应事件 ******/
        private void queryIndexComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //将下拉框中选中的图层序号设为操作图层序号
            layerIndex = queryIndexComboBox1.SelectedIndex;
            //将当前选中的图层设置为编辑的图层
            currentEditingLayer = this.axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
        }

        /****** 改变选择叠置图层下拉框的响应事件 ******/
        private void intersectIndexBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //将下拉框中选中的图层序号设为叠置图层序号
            intersectLayerIndex = intersectIndexBox1.SelectedIndex;
        }

        /****** 鹰眼地图控件的鼠标按下的响应事件 ******/
        private void axMapControl2_OnMouseDown(object sender, IMapControlEvents2_OnMouseDownEvent e)
        {
            //左键在鹰眼地图中画一个矩形，将主控件的视图范围缩放到该矩形范围内
            if (e.button == 1)
            {
                IEnvelope pEnv = axMapControl2.TrackRectangle();
                axMapControl1.Extent = pEnv;
                axMapControl1.Refresh();
            }

            //右键在鹰眼地图中点击，可以用同样比例的视图范围移动到该点处
            else if (e.button == 2)
            {
                IPoint pPoint = new ESRI.ArcGIS.Geometry.Point();
                pPoint.PutCoords(e.mapX, e.mapY);
                axMapControl1.CenterAt(pPoint);
                axMapControl1.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            }
        }

        /****** 属性查询按钮的鼠标响应事件 ******/
        private void attributeQueryButton_Click(object sender, EventArgs e)
        {
            //获取数据
            ILayer pLayer = axMapControl1.get_Layer(layerIndex);
            IFeatureLayer pFeatureLyr = pLayer as IFeatureLayer;
            if (pFeatureLyr == null) { return; }//转换不成功返回

            //SQL语句
            IQueryFilter pQueryFilter = new QueryFilter();
            //pQueryFilter.WhereClause = "STATE_NAME LIKE 'Al%'";//字段名 比较符 值
            if (SQLTextBox1.Text == "")
            {
                MessageBox.Show("请输入SQL语句！");
                return;
            }
            pQueryFilter.WhereClause = SQLTextBox1.Text;

            //查询语句执行
            IFeatureCursor pFeatureCursor = pFeatureLyr.Search(pQueryFilter, true);

            //查询结果输出
            IFeature pFeature = pFeatureCursor.NextFeature();
            IPoint pNewCnt = new ESRI.ArcGIS.Geometry.Point();

            while (pFeature != null)
            {

                //闪烁3次，间隔300ms
                axMapControl1.FlashShape(pFeature.Shape, 3, 300, null);
                //将查询结果加到数据集中
                axMapControl1.Map.SelectFeature(pLayer, pFeature);
                pFeature = pFeatureCursor.NextFeature();
            }
            axMapControl1.Refresh();
        }

        /****** 缓冲区分析的菜单鼠标点击响应事件 ******/
        private void bufferAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //判断主地图控件中是否包含图层
            if (this.axMapControl1.LayerCount == 0)
                return;

            MessageBox.Show("将会对操作图层中的所有要素生成缓冲区");

            //根据所选择的图层参数layerIndex获取主地图控件中的图层
            ILayer pLayer = this.axMapControl1.Map.get_Layer(layerIndex);
            //输出路径,可以自行指定
            string layerName = this.axMapControl1.Map.get_Layer(layerIndex).Name;
            string strOutputPath = @"..\data\bufferResults\" + layerName + "_Buffer.shp";
            //缓冲半径
            double bufferDistace = 0.5;

            //获取一个geoprocessor的实例,避免与命名空间Geoprocessing中的Geoprocessor发生引用错误
            ESRI.ArcGIS.Geoprocessor.Geoprocessor pGeoprocessor = new ESRI.ArcGIS.Geoprocessor.Geoprocessor();
            //OverwriteOutput为真时，输出图层会覆盖当前文件夹下的同名图层
            pGeoprocessor.OverwriteOutput = true;
            //创建一个Buffer工具的实例
            ESRI.ArcGIS.AnalysisTools.Buffer buffer = new ESRI.ArcGIS.AnalysisTools.Buffer(pLayer, strOutputPath, bufferDistace);
            //执行缓冲区分析
            IGeoProcessorResult bufferResults = null;
            bufferResults = pGeoprocessor.Execute(buffer, null) as IGeoProcessorResult;

            //判断缓冲区是否成功生成
            if (bufferResults.Status != esriJobStatus.esriJobSucceeded)
            {
                MessageBox.Show("图层" + pLayer.Name + "缓冲区生成失败！");
            }
            else
            {
                MessageBox.Show("缓冲区生成成功！");
                //将生成图层加入主地图控件
                int index = strOutputPath.LastIndexOf("\\");
                this.axMapControl1.AddShapeFile(strOutputPath.Substring(0, index), strOutputPath.Substring(index));
            }

        }

        /****** 叠置分析的菜单鼠标点击响应事件 ******/
        private void intersectAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //添加两个以上图层时才允许叠置
            if (this.axMapControl1.LayerCount < 2) { return; }

            ESRI.ArcGIS.Geoprocessor.Geoprocessor pGeoprocessor = new ESRI.ArcGIS.Geoprocessor.Geoprocessor();
            //OverwriteOutput为真时，输出图层会覆盖当前文件夹下的同名图层
            pGeoprocessor.OverwriteOutput = true;

            //创建叠置分析实例
            Intersect intersectTool = new Intersect();
            //获取MapControl中的前两个图层
            ILayer pInputLayer1 = this.axMapControl1.get_Layer(layerIndex);
            ILayer pInputLayer2 = this.axMapControl1.get_Layer(intersectLayerIndex);
            //转换为object类型
            object inputfeature1 = pInputLayer1;
            object inputfeature2 = pInputLayer2;
            //设置参与叠置分析的多个对象
            IGpValueTableObject pObject = new GpValueTableObject();
            pObject.SetColumns(2);
            pObject.AddRow(ref inputfeature1);
            pObject.AddRow(ref inputfeature2);
            intersectTool.in_features = pObject;
            //设置输出路径
            string strTempPath = @"..\data\intersectResults\";
            string strOutputPath = strTempPath + pInputLayer1.Name + "_" + pInputLayer2.Name + "_Intersect.shp";
            intersectTool.out_feature_class = strOutputPath;
            //执行叠置分析
            IGeoProcessorResult result = null;
            result = pGeoprocessor.Execute(intersectTool, null) as IGeoProcessorResult;

            //判断叠置分析是否成功
            if (result.Status != ESRI.ArcGIS.esriSystem.esriJobStatus.esriJobSucceeded)
                MessageBox.Show("叠置求交失败!");
            else
            {
                MessageBox.Show("叠置求交成功！");
                int index = strOutputPath.LastIndexOf("\\");
                this.axMapControl1.AddShapeFile(strOutputPath.Substring(0, index), strOutputPath.Substring(index));
            }

        }

        /****** 开始地图编辑的菜单鼠标点击响应事件 ******/
        private void startEditingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //判断是否存在可编辑图层
            if (this.axMapControl1.Map.LayerCount == 0) { return; }
            //编辑图层默认为操作图层下拉框（queryIndexComboBox1）所选择的图层，图层序号为：layerIndex

            //将地图功能切换成地图编辑
            m_OperationType = functionOperationType.Edit;

            //鼠标形状变成箭头
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrow;

            //获取地图和编辑图层
            currentEditingMap = this.axMapControl1.Map;
            currentEditingLayer = this.axMapControl1.get_Layer(layerIndex) as IFeatureLayer;

            //获取要素工作空间
            IFeatureClass pFeatureClass = currentEditingLayer.FeatureClass;
            IWorkspace pWorkspace = (pFeatureClass as IDataset).Workspace;
            currentEditingWorkspace = pWorkspace as IWorkspaceEdit;
            if (currentEditingWorkspace == null) { return; }

            //开始编辑
            if (!currentEditingWorkspace.IsBeingEdited())
            {
                currentEditingWorkspace.StartEditing(true);
                isEditing = true;
            }

            //开始编辑设为不可用，将其他编辑菜单项设为可用
            this.startEditingToolStripMenuItem.Enabled = false;
            this.selectEditingOperationToolStripMenuItem.Enabled = true;
            this.saveEditingResultToolStripMenuItem.Enabled = true;
            this.finishEditingToolStripMenuItem.Enabled = true;
        }

        /****** 保存地图编辑结果的菜单鼠标点击响应事件 ******/
        private void saveEditingResultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //处于编辑状态且已编辑则保存
            if (isEditing && haveEditing)
            {
                currentEditingWorkspace.StopEditing(true);
                haveEditing = false;
            }
            else
            {
                currentEditingWorkspace.StopEditing(false);
            }
        }

        /****** 结束地图编辑的菜单鼠标点击响应事件 ******/
        private void finishEditingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (haveEditing)
            {
                DialogResult pdialogResult = MessageBox.Show("图层已编辑，是否保存？", "提示", MessageBoxButtons.OKCancel);
                if (pdialogResult == DialogResult.OK)
                {
                    //处于编辑状态且已编辑则保存
                    if (isEditing && haveEditing)
                    {
                        currentEditingWorkspace.StopEditing(true);
                        haveEditing = false;
                    }
                    else 
                    {
                        currentEditingWorkspace.StopEditing(false);
                    }
                }
                isEditing = false;
            }
            //将地图编辑的选择操作类型、保存编辑结果、结束编辑菜单设置为不可选
            this.startEditingToolStripMenuItem.Enabled = true;
            this.selectEditingOperationToolStripMenuItem.Enabled = false;
            this.saveEditingResultToolStripMenuItem.Enabled = false;
            this.finishEditingToolStripMenuItem.Enabled = false;
        }

        /****** 选择地图编辑方式-创建要素的菜单鼠标点击响应事件 ******/
        private void createFeaturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_editOperationType = editOperationType.Create;
        }

        /****** 选择地图编辑方式-移动要素的菜单鼠标点击响应事件 ******/
        private void moveFeaturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_editOperationType = editOperationType.Move;
        }

        /****** 向当前图层添加几何要素的功能函数 ******/
        public void AddFeature(IGeometry geometry)
        {
            currentEditingLayer = this.axMapControl1.get_Layer(layerIndex) as IFeatureLayer;
            //判断当前编辑图层是否为空
            if (currentEditingLayer == null) return;
            IFeatureClass pFeatureClass = currentEditingLayer.FeatureClass;
            //几何要素与要素类类型一致
            if (pFeatureClass.ShapeType != geometry.GeometryType || geometry == null)
                return;
            //判断编辑状态
            if (!isEditing)
            {
                MessageBox.Show("请先开启编辑", "提示");
                return;
            }
            try
            {
                //开始编辑操作
                currentEditingWorkspace.StartEditOperation();
                //创建要素并保存
                IFeature pFeature;
                pFeature = pFeatureClass.CreateFeature();
                pFeature.Shape = geometry;
                pFeature.Store();
                //结束编辑操作
                currentEditingWorkspace.StopEditOperation();
                //高亮显示添加要素
                currentEditingMap.ClearSelection();
                currentEditingMap.SelectFeature(currentEditingLayer as ILayer, pFeature);
                ((IActiveView)currentEditingMap).PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
                haveEditing = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /****** 点击选中要素的功能函数 ******/
        public IFeature SelectFeature(IPoint point)
        {
            if (point == null)
                return null;

            //根据点击位置生成空间查询几何要素
            IActiveView pActiveView = currentEditingMap as IActiveView;
            //屏幕距离转换为地图距离
            double dblDistance = ConvertPixelToMapUnits(pActiveView, 2);
            ITopologicalOperator pTopo = point as ITopologicalOperator;
            IGeometry pGeoBuffer = pTopo.Buffer(dblDistance);


            //定义空间过滤器
            ISpatialFilter pSpatialFilter = new SpatialFilter();
            //空间过滤器参数设置
            pSpatialFilter.Geometry = pGeoBuffer;
            pSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
            pSpatialFilter.GeometryField = currentEditingLayer.FeatureClass.ShapeFieldName;

            IFeatureClass pFeatureClass = currentEditingLayer.FeatureClass;
            //空间查询
            IFeatureCursor pFeatureCursor = pFeatureClass.Search(pSpatialFilter, false);
            IFeature pFeature = pFeatureCursor.NextFeature();
            if (pFeature == null)
                return null;
            //要素设为选中状态
            currentEditingMap.SelectFeature(currentEditingLayer as ILayer, pFeature);
            ((IActiveView)currentEditingMap).PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
            return pFeature;

        }

        /****** 更新要素形状的功能函数 ******/
        private void UpdateFeature(IFeature feature, IGeometry geometry)
        {
            //确定当前图层处在编辑状态
            if (!currentEditingWorkspace.IsBeingEdited())
            {
                MessageBox.Show("当前图层不在编辑状态", "提示");
                return;
            }

            //开始编辑操作
            currentEditingWorkspace.StartEditOperation();
            //更新要素形状
            feature.Shape = geometry;
            feature.Store();
            //结束编辑操作
            currentEditingWorkspace.StopEditOperation();
            haveEditing = true;
        }

        /****** 清除地图上显示的选择要素集的功能函数 ******/
        public void ClearSelection()
        {
            ((IActiveView)currentEditingMap).PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
            currentEditingMap.ClearSelection();
            ((IActiveView)currentEditingMap).PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
        }

        /****** 实现屏幕距离向地图距离的转化的功能函数 ******/
        private double ConvertPixelToMapUnits(IActiveView activeView, double pixelUnits)
        {
            double realWorldDiaplayExtent;
            int pixelExtent;
            double sizeOfOnePixel;
            double mapUnits;

            //获取设备中视图显示宽度，即像素个数
            pixelExtent = activeView.ScreenDisplay.DisplayTransformation.get_DeviceFrame().right - activeView.ScreenDisplay.DisplayTransformation.get_DeviceFrame().left;
            //获取地图坐标系中地图显示范围
            realWorldDiaplayExtent = activeView.ScreenDisplay.DisplayTransformation.VisibleBounds.Width;
            //每个像素大小代表的实际距离
            sizeOfOnePixel = realWorldDiaplayExtent / pixelExtent;
            //地理距离
            mapUnits = pixelUnits * sizeOfOnePixel;

            return mapUnits;
        }

        /****** 加载几何网络文件的菜单鼠标点击响应事件 ******/
        private void loadNetworkFileToolStripMenuItem_Click(object sender, EventArgs e)
        {

            //获取几何网络文件路径
            //注意修改此路径为当前存储路径
            string strPath = @"..\data\USHIGH\USA_Highway_Network_GDB.mdb";
            //打开工作空间
            IWorkspaceFactory pWorkspaceFactory = new AccessWorkspaceFactory();
            IFeatureWorkspace pFeatureWorkspace = pWorkspaceFactory.OpenFromFile(strPath, 0) as IFeatureWorkspace;
            //获取要素数据集
            //注意名称的设置要与上面创建保持一致
            IFeatureDataset pFeatureDataset = pFeatureWorkspace.OpenFeatureDataset("Highway");

            //获取network集合
            INetworkCollection pNetWorkCollection = pFeatureDataset as INetworkCollection;
            //获取network的数量,为零时返回
            int intNetworkCount = pNetWorkCollection.GeometricNetworkCount;
            if (intNetworkCount < 1)
                return;
            //FeatureDataset可能包含多个network，我们获取指定的network
            //注意network的名称的设置要与上面创建保持一致
            mGeometricNetwork = pNetWorkCollection.get_GeometricNetworkByName("Highway_net");

            //将Network中的每个要素类作为一个图层加入地图控件
            IFeatureClassContainer pFeatClsContainer = mGeometricNetwork as IFeatureClassContainer;
            //获取要素类数量，为零时返回
            int intFeatClsCount = pFeatClsContainer.ClassCount;
            if (intFeatClsCount < 1)
                return;
            IFeatureClass pFeatureClass;
            IFeatureLayer pFeatureLayer;
            for (int i = 0; i < intFeatClsCount; i++)
            {
                //获取要素类
                pFeatureClass = pFeatClsContainer.get_Class(i);
                pFeatureLayer = new FeatureLayer();
                pFeatureLayer.FeatureClass = pFeatureClass;
                pFeatureLayer.Name = pFeatureClass.AliasName;
                //加入地图控件
                this.axMapControl1.AddLayer((ILayer)pFeatureLayer, 0);
            }

            //计算snap tolerance为图层最大宽度的1/100
            //获取图层数量
            int intLayerCount = this.axMapControl1.LayerCount;
            IGeoDataset pGeoDataset;
            IEnvelope pMaxEnvelope = new EnvelopeClass();
            for (int i = 0; i < intLayerCount; i++)
            {
                //获取图层
                pFeatureLayer = this.axMapControl1.get_Layer(i) as IFeatureLayer;
                pGeoDataset = pFeatureLayer as IGeoDataset;
                //通过Union获得较大图层范围
                pMaxEnvelope.Union(pGeoDataset.Extent);
            }
            double dblWidth = pMaxEnvelope.Width;
            double dblHeight = pMaxEnvelope.Height;
            double dblSnapTol;
            if (dblHeight < dblWidth)
                dblSnapTol = dblWidth * 0.01;
            else
                dblSnapTol = dblHeight * 0.01;

            //设置源地图，几何网络以及捕捉容差
            mPointToEID = new PointToEID();
            mPointToEID.SourceMap = this.axMapControl1.Map;
            mPointToEID.GeometricNetwork = mGeometricNetwork;
            mPointToEID.SnapTolerance = dblSnapTol;

            //将操作图层和叠置图层下拉框中的选项清空
            queryIndexComboBox1.Items.Clear();
            intersectIndexBox1.Items.Clear();
            //基于主地图控件中的所有图层循环向查询图层下拉框中添加选项
            for (int i = 0; i < intLayerCount; i++)
            {
                queryIndexComboBox1.Items.Add(this.axMapControl1.get_Layer(i).Name);
                queryIndexComboBox1.SelectedIndex = 0;
                intersectIndexBox1.Items.Add(this.axMapControl1.get_Layer(i).Name);
                intersectIndexBox1.SelectedIndex = 0;
            }
        }

        /****** 开始进行网络分析的菜单鼠标点击响应事件 ******/
        private void startNetworkAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_OperationType = functionOperationType.networkAnalysis;
            //鼠标形状变成箭头
            axMapControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrow;
        }

        /****** 结束网络分析的菜单鼠标点击响应事件 ******/
        private void finishNetworkAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_OperationType = functionOperationType.None;
        }

        /****** 路径计算的功能函数 ******/
        private void SolvePath(string weightName)
        {
            //创建ITraceFlowSolverGEN，ITraceFlowSolverGEN是几何网络分析中的重要接口
            ITraceFlowSolverGEN pTraceFlowSolverGEN = new TraceFlowSolverClass();
            INetSolver pNetSolver = pTraceFlowSolverGEN as INetSolver;
            //初始化用于路径计算的Network
            INetwork pNetWork = mGeometricNetwork.Network;
            pNetSolver.SourceNetwork = pNetWork;

            //获取分析经过的点的个数
            int intCount = mPointCollection.PointCount;
            //当输入点点集中没有点时返回
            if (intCount < 1) { return; }


            INetFlag pNetFlag;
            //基于输入点的数量建立用于存储路径计算得到的边的数组
            IEdgeFlag[] pEdgeFlags = new IEdgeFlag[intCount];

            //建立节点
            IPoint pEdgePoint = new PointClass();
            int intEdgeEID;
            IPoint pFoundEdgePoint;
            double dblEdgePercent;

            //用于获取几何网络元素的UserID, UserClassID,UserSubID
            INetElements pNetElements = pNetWork as INetElements;
            int intEdgeUserClassID;
            int intEdgeUserID;
            int intEdgeUserSubID;
            for (int i = 0; i < intCount; i++)
            {
                pNetFlag = new EdgeFlagClass();
                //获取距离用户点击点最近的节点
                pEdgePoint = mPointCollection.get_Point(i);
                //查找距离节点最近的边
                mPointToEID.GetNearestEdge(pEdgePoint, out intEdgeEID, out pFoundEdgePoint, out dblEdgePercent);
                //如果找不到，则跳出进入下一个循环
                if (intEdgeEID <= 0) { continue; }
                //根据得到的边查询对应的几何网络中的元素UserID, UserClassID,UserSubID
                pNetElements.QueryIDs(intEdgeEID, esriElementType.esriETEdge,out intEdgeUserClassID, out intEdgeUserID, out intEdgeUserSubID);
                //查询失败，跳出进入下一循环
                if (intEdgeUserClassID <= 0 || intEdgeUserID <= 0) { continue; }

                pNetFlag.UserClassID = intEdgeUserClassID;
                pNetFlag.UserID = intEdgeUserID;
                pNetFlag.UserSubID = intEdgeUserSubID;
                pEdgeFlags[i] = pNetFlag as IEdgeFlag;
            }
            //设置路径求解的边
            pTraceFlowSolverGEN.PutEdgeOrigins(ref pEdgeFlags);

            //路径计算权重
            INetSchema pNetSchema = pNetWork as INetSchema;
            INetWeight pNetWeight = pNetSchema.get_WeightByName(weightName);
            if (pNetWeight == null) { return; }

            //设置权重，这里双向的权重设为一致
            INetSolverWeights pNetSolverWeights = pTraceFlowSolverGEN as INetSolverWeights;
            pNetSolverWeights.ToFromEdgeWeight = pNetWeight;
            pNetSolverWeights.FromToEdgeWeight = pNetWeight;

            object[] arrResults = new object[intCount - 1];
            //执行路径计算
            pTraceFlowSolverGEN.FindPath(esriFlowMethod.esriFMConnected, esriShortestPathObjFn.esriSPObjFnMinSum, out mEnumNetEID_Junctions, out mEnumNetEID_Edges, intCount - 1, ref arrResults);

            //获取路径计算总代价（cost）
            mdblPathCost = 0;
            for (int i = 0; i < intCount - 1; i++)
                mdblPathCost += (double)arrResults[i];
        }

        /****** 路径转换为几何要素的功能函数 ******/
        private IPolyline PathToPolyLine()
        {
            IPolyline pPolyLine = new PolylineClass();
            IGeometryCollection pNewGeometryCollection = pPolyLine as IGeometryCollection;
            if (mEnumNetEID_Edges == null) { return null; }

            IEIDHelper pEIDHelper = new EIDHelper();
            //获取几何网络
            pEIDHelper.GeometricNetwork = mGeometricNetwork;
            //获取地图空间参考
            ISpatialReference pSpatialReference = this.axMapControl1.Map.SpatialReference;
            pEIDHelper.OutputSpatialReference = pSpatialReference;
            pEIDHelper.ReturnGeometries = true;
            //根据边的ID获取边的信息
            IEnumEIDInfo pEnumEIDInfo = pEIDHelper.CreateEnumEIDInfo(mEnumNetEID_Edges);
            int intCount = pEnumEIDInfo.Count;
            pEnumEIDInfo.Reset();

            IEIDInfo pEIDInfo;
            IGeometry pGeometry;
            for (int i = 0; i < intCount; i++)
            {
                pEIDInfo = pEnumEIDInfo.Next();
                //获取边的几何要素
                pGeometry = pEIDInfo.Geometry;
                pNewGeometryCollection.AddGeometryCollection((IGeometryCollection)pGeometry);
            }
            return pPolyLine;
        }

        /****** 地图二三维选项卡切换时图层空间绑定更改的响应事件 ******/
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0) { this.axTOCControl1.SetBuddyControl(axMapControl1.Object); }
            else if (tabControl1.SelectedIndex == 1) { this.axTOCControl1.SetBuddyControl(axSceneControl1.Object); }
        }

        /****** 加载sxd文档的菜单鼠标点击响应事件 ******/
        private void loadSxdFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //打开文件资源管理器选择加载的文件
            OpenFileDialog sxdOpenDialog = new OpenFileDialog();
            sxdOpenDialog.Filter = "sxd文件|*.sxd";
            //打开文件对话框打开事件
            if (sxdOpenDialog.ShowDialog() == DialogResult.OK)
            {
                //从打开对话框中得到打开文件的全路径,并将该路径传入到axSceneControl1中
                axSceneControl1.LoadSxFile(sxdOpenDialog.FileName);
            }
            //将控件置于顶层
            axSceneControl1.BringToFront();
        }

        /****** 加载栅格文件的菜单鼠标点击响应事件 ******/
        private void loadRasterFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sFileName = null;
            //新建栅格图层
            IRasterLayer pRasterLayer = new RasterLayerClass();

            //打开文件资源管理器选择加载的文件
            OpenFileDialog rasterLayerOpenDialog = new OpenFileDialog();
            rasterLayerOpenDialog.Filter = "所有文件|*.*";

            //打开文件对话框打开事件
            if (rasterLayerOpenDialog.ShowDialog() == DialogResult.OK)
            {
                //从打开对话框中得到打开文件的全路径
                sFileName = rasterLayerOpenDialog.FileName;
                //创建栅格图层
                pRasterLayer.CreateFromFilePath(sFileName);
                //将图层加入到控件中
                axSceneControl1.Scene.AddLayer(pRasterLayer, true);


                //将当前视点跳转到栅格图层
                ICamera pCamera = axSceneControl1.Scene.SceneGraph.ActiveViewer.Camera;
                //得到范围
                IEnvelope pEenvelop = pRasterLayer.VisibleExtent;
                //添加z轴上的范围
                pEenvelop.ZMin = axSceneControl1.Scene.Extent.ZMin;
                pEenvelop.ZMax = axSceneControl1.Scene.Extent.ZMax;
                //设置相机
                pCamera.SetDefaultsMBB(pEenvelop);
                axSceneControl1.Refresh();
            }

            //将控件置于顶层
            axSceneControl1.BringToFront();
        }

        /****** 保存图片文件的菜单鼠标点击响应事件 ******/
        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sFileName = "";
            //打开文件资源管理器选择要保存成的文件
            SaveFileDialog imageSaveDialog = new SaveFileDialog();
            //保存对话框的标题
            imageSaveDialog.Title = "保存图片";
            //保存对话框过滤器
            imageSaveDialog.Filter = "BMP图片|*.bmp|JPG图片|*.jpg";
            //图片的高度和宽度
            int Width = axSceneControl1.Width;
            int Height = axSceneControl1.Height;
            if (imageSaveDialog.ShowDialog() == DialogResult.OK)
            {
                sFileName = imageSaveDialog.FileName;
                if (imageSaveDialog.FilterIndex == 1)//保存成BMP格式的文件
                {
                    axSceneControl1.SceneViewer.GetSnapshot(Width, Height,
                        esri3DOutputImageType.BMP, sFileName);
                }
                else//保存成JPG格式的文件
                {
                    axSceneControl1.SceneViewer.GetSnapshot(Width, Height,
                        esri3DOutputImageType.JPEG, sFileName);
                }
                MessageBox.Show("保存图片成功！");
                axSceneControl1.Refresh();
            }
        }

        /****** 三维视图-恢复箭头的菜单鼠标点击响应事件 ******/
        private void d3ArrowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //将当前所有操作清除，鼠标形状恢复成箭头
            axSceneControl1.CurrentTool = null;
            axSceneControl1.MousePointer = ESRI.ArcGIS.Controls.esriControlsMousePointer.esriPointerArrow;
        }

        /****** 三维视图放大的菜单鼠标点击响应事件 ******/
        private void d3ZoomInToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //创建命令
            ICommand pCommand = new ControlsSceneZoomInTool();
            pCommand.OnCreate(axSceneControl1.Object);
            //将当前工具设置为放大工具
            axSceneControl1.CurrentTool = pCommand as ITool;
            pCommand = null;
            //刷新
            axSceneControl1.Refresh();
        }

        /****** 三维视图缩小的菜单鼠标点击响应事件 ******/
        private void d3ZoomOutToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //创建命令
            ICommand pCommand = new ControlsSceneZoomOutTool();
            pCommand.OnCreate(axSceneControl1.Object);
            //将当前工具设置为缩小工具
            axSceneControl1.CurrentTool = pCommand as ITool;
            pCommand = null;
            //刷新
            axSceneControl1.Refresh();
        }

        /****** 三维视图平移的菜单鼠标点击响应事件 ******/
        private void d3PanToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //创建命令
            ICommand pCommand = new ControlsScenePanTool();
            pCommand.OnCreate(axSceneControl1.Object);
            //将当前工具设置为平移工具
            axSceneControl1.CurrentTool = pCommand as ITool;
            pCommand = null;
            //刷新
            axSceneControl1.Refresh();
        }

        /****** 三维视图缩放至全图的菜单鼠标点击响应事件 ******/
        private void d3FullExtentCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //创建命令
            ICommand pCommand = new ControlsSceneFullExtentCommand();
            pCommand.OnCreate(axSceneControl1.Object);
            pCommand.OnClick();
            pCommand = null;
            //刷新
            axSceneControl1.Refresh();
        }

        /****** 三维飞行的菜单鼠标点击响应事件 ******/
        private void d3FlyToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //创建命令
            ICommand pCommand = new ControlsSceneFlyTool();
            pCommand.OnCreate(axSceneControl1.Object);
            //将当前工具设置为飞行工具
            axSceneControl1.CurrentTool = pCommand as ITool;
            pCommand = null;
            //刷新
            axSceneControl1.Refresh();
        }

        /****** 三维导航的菜单鼠标点击响应事件 ******/
        private void d3NavigateToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //创建命令
            ICommand pCommand = new ControlsSceneNavigateTool();
            pCommand.OnCreate(axSceneControl1.Object);
            //将当前工具设置为导航工具
            axSceneControl1.CurrentTool = pCommand as ITool;
            pCommand = null;
            //刷新
            axSceneControl1.Refresh();
        }

        /****** 选择生成TIN的图层的菜单鼠标点击响应事件 ******/
        private void tinSelectLayerComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            tinSelectFieldComboBox.Items.Clear();

            //获取图层数
            int nCount = axSceneControl1.Scene.LayerCount;
            ILayer pLayer = null;
            IFeatureLayer pFeatureLayer = null;
            IFields pField = null;

            //寻找名称为layerName的FeatureLayer;
            for (int i = 0; i < nCount; i++)
            {
                pLayer = axSceneControl1.Scene.get_Layer(i) as IFeatureLayer;
                //找到了layerName的Featurelayer
                if (pLayer.Name == tinSelectLayerComboBox.Items[tinSelectLayerComboBox.SelectedIndex].ToString())
                {
                    pFeatureLayer = pLayer as IFeatureLayer;
                    pField = pFeatureLayer.FeatureClass.Fields;
                    //图层中的字段数
                    nCount = pField.FieldCount;
                    //将该图层中所用的字段写入到tinSelectFieldComboBox中去
                    for (int j = 0; j < nCount; j++)
                    {
                        tinSelectFieldComboBox.Items.Add(pField.get_Field(j).Name);
                    }
                    break;
                }
            }
            tinSelectFieldComboBox.SelectedIndex = 0;
        }

        /****** 刷新三维图层的鼠标点击响应事件 ******/
        private void refresh3DLayersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tinSelectLayerComboBox.Items.Clear();
            //得到当前场景中所有图层
            int nCount = axSceneControl1.Scene.LayerCount;
            if (nCount <= 0)//没有图层的情况
            {
                MessageBox.Show("场景中没有图层，请加入图层");
                return;
            }
            ILayer pLayer = null;
            //将所有的图层的名称显示到复选框中
            for (int i = 0; i < nCount; i++)
            {
                pLayer = axSceneControl1.Scene.get_Layer(i);
                tinSelectLayerComboBox.Items.Add(pLayer.Name);
            }
            //将复选框设置为选中第一项
            tinSelectLayerComboBox.SelectedIndex = 0;

            tinSelectFieldComboBox.Items.Clear();
            IFeatureLayer pFeatureLayer = null;
            IFields pField = null;
            //寻找名称为layerName的FeatureLayer;
            for (int i = 0; i < nCount; i++)
            {
                pLayer = axSceneControl1.Scene.get_Layer(i) as IFeatureLayer;
                //找到了layerName的Featurelayer
                if (pLayer.Name == tinSelectLayerComboBox.Items[tinSelectLayerComboBox.SelectedIndex].ToString())
                {
                    pFeatureLayer = pLayer as IFeatureLayer;
                    pField = pFeatureLayer.FeatureClass.Fields;
                    nCount = pField.FieldCount;
                    //将该图层中所用的字段写入到tinSelectFieldComboBox中去
                    for (int j = 0; j < nCount; j++)
                    {
                        tinSelectFieldComboBox.Items.Add(pField.get_Field(j).Name);
                    }
                    break;
                }
            }
            tinSelectFieldComboBox.SelectedIndex = 0;

        }

        /****** 生成TIN的菜单鼠标点击响应事件 ******/
        private void constructTINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //判断是否已经选择了生成TIN的图层
            if (tinSelectLayerComboBox.Text == "" || tinSelectFieldComboBox.Text == "")
            {
                MessageBox.Show("没有相应的图层，请刷新图层后选择");
                return;
            }
            ITinEdit pTin = new TinClass();
            //寻找Featurelayer
            IFeatureLayer pFeatureLayer =
                axSceneControl1.Scene.get_Layer(tinSelectLayerComboBox.SelectedIndex) as IFeatureLayer;
            if (pFeatureLayer != null)
            {
                IEnvelope pEnvelope = new EnvelopeClass();
                IFeatureClass pFeatureClass = pFeatureLayer.FeatureClass;
                IQueryFilter pQueryFilter = new QueryFilterClass();
                IField pField = null;
                //找字段
                pField = pFeatureClass.Fields.get_Field(pFeatureClass.Fields.FindField(tinSelectFieldComboBox.Text));
                //判断字段类型
                if (pField.Type == esriFieldType.esriFieldTypeInteger ||
                     pField.Type == esriFieldType.esriFieldTypeDouble ||
                     pField.Type == esriFieldType.esriFieldTypeSingle)
                {
                    IGeoDataset pGeoDataset = pFeatureLayer as IGeoDataset;
                    pEnvelope = pGeoDataset.Extent;
                    //设置空间参考系
                    ISpatialReference pSpatialReference;
                    pSpatialReference = pGeoDataset.SpatialReference;
                    //选择生成TIN的输入类型
                    esriTinSurfaceType pSurfaceTypeCount = esriTinSurfaceType.esriTinMassPoint;
                    switch (tinSelectTypeComboBox.Text)
                    {
                        case "点":
                            pSurfaceTypeCount = esriTinSurfaceType.esriTinMassPoint;
                            break;
                        case "直线":
                            pSurfaceTypeCount = esriTinSurfaceType.esriTinSoftLine;
                            break;
                        case "光滑线":
                            pSurfaceTypeCount = esriTinSurfaceType.esriTinHardLine;
                            break;
                    }
                    //创建TIN
                    pTin.InitNew(pEnvelope);
                    object missing = Type.Missing;
                    //生成TIN
                    pTin.AddFromFeatureClass(pFeatureClass, pQueryFilter, pField, pField, pSurfaceTypeCount, ref missing);
                    pTin.SetSpatialReference(pGeoDataset.SpatialReference);
                    //创建Tin图层并将Tin图层加入到场景中去
                    ITinLayer pTinLayer = new TinLayerClass();
                    pTinLayer.Dataset = pTin as ITin;
                    axSceneControl1.Scene.AddLayer(pTinLayer, true);
                }
                else
                {
                    MessageBox.Show("该字段的类型不符合构建TIN的条件");
                }
            }
        }

        /****** 进入三维点查询的菜单鼠标点击响应事件 ******/
        private void start3DPointQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            is3DPointQuery = true;
        }

        /****** 退出三维点查询的菜单鼠标点击响应事件 ******/
        private void finish3DPointQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            is3DPointQuery = false;
        }

        /****** ArcScene控件的鼠标按下的响应事件 ******/
        private void axSceneControl1_OnMouseDown(object sender, ISceneControlEvents_OnMouseDownEvent e)
        {
            if (is3DPointQuery)//check按钮处于打勾状态
            {
                //点查询接口
                IHit3DSet mHit3DSet;
                d3QueryResultForm md3QueryResultForm = new d3QueryResultForm();
                //查询
                axSceneControl1.SceneGraph.LocateMultiple(axSceneControl1.SceneGraph.ActiveViewer, e.x, e.y, esriScenePickMode.esriScenePickAll, false, out mHit3DSet);
                mHit3DSet.OnePerLayer();
                if (mHit3DSet == null)//没有选中对象
                {
                    MessageBox.Show("没有选中对象");
                }
                else
                {
                    //显示在ResultForm控件中。
                    md3QueryResultForm.Show();
                    md3QueryResultForm.refeshView(mHit3DSet);
                }
                axSceneControl1.Refresh();
            }
        }

        /****** ArcScene控件的鼠标滚轮缩放的功能函数 ******/
        private void axSceneControl1_wheelZoom(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (axSceneControl1.Visible == true)
            {
                System.Drawing.Point pSceLoc = axSceneControl1.PointToScreen(this.axSceneControl1.Location);
                System.Drawing.Point Pt = this.PointToScreen(e.Location);
                if (Pt.X < pSceLoc.X | Pt.X > pSceLoc.X + axSceneControl1.Width | Pt.Y < pSceLoc.Y | Pt.Y > pSceLoc.Y + axSceneControl1.Height) return;
                double scale = 0.2;
                //if (e.Delta < 0) scale = -0.2;
                if (e.Delta > 0) scale = -0.2;
                ICamera pCamera = axSceneControl1.Camera;
                IPoint pPtObs = pCamera.Observer;
                IPoint pPtTar = pCamera.Target;
                pPtObs.X += (pPtObs.X - pPtTar.X) * scale;
                pPtObs.Y += (pPtObs.Y - pPtTar.Y) * scale;
                pPtObs.Z += (pPtObs.Z - pPtTar.Z) * scale;
                pCamera.Observer = pPtObs;
                axSceneControl1.SceneGraph.RefreshViewers();
            }
        }

    }
}
