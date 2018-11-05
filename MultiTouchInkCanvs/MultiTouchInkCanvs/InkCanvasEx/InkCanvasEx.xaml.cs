
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MultiTouchInkCanvs
{
    /// <summary>
    /// InkCanvasEx.xaml 的交互逻辑
    /// </summary>  
    public partial class InkCanvasEx : UserControl
    {
        private Dictionary<object, StrokeCollection> _strokes = new Dictionary<object, StrokeCollection>();
        private Dictionary<object, Stroke> _currentStroke = new Dictionary<object, Stroke>();  
        private Dictionary<TouchDevice, Point> TouchInScreen = new Dictionary<TouchDevice, Point>(); //Save TouchDevice

        private int touchcount;
        public int TouchCount
        {
            get { return this.touchcount = TouchInScreen.Count; }
        }
        
        public StrokeCollection InkCanvasStrokes
        {
            get { return (StrokeCollection)GetValue(InkCanvasStrokesProperty); }
            set { SetValue(InkCanvasStrokesProperty, value); }
        }

        private StrokeCollection inkStrokes = new StrokeCollection();
        // Using a DependencyProperty as the backing store for InkCanvasStrokes.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InkCanvasStrokesProperty =
            DependencyProperty.Register("InkCanvasStrokes", typeof(StrokeCollection), typeof(InkCanvasEx), new PropertyMetadata(InkCanvasPropertyChangedCallback));

        private static void InkCanvasPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            InkCanvasEx control = (d as InkCanvasEx);
            if (control != null)
            {
                control.Inkcanvas.Strokes = ((StrokeCollection)e.NewValue);
            }
        }

        public struct ThreadPoint
        {
            public object device;  // device
            public Point inputPosition;     // point         
        }
        ThreadPoint p;

        private static Queue<ThreadPoint> qData = new Queue<ThreadPoint>();//Queue
        private bool m_bRuning = true;
        private static AutoResetEvent mEvent = new AutoResetEvent(false);//Message
        private static System.Object lockThis = new System.Object();
        private Thread thread;

        public delegate void DataCallBack(ref ThreadPoint test);
        DataCallBack dbcall = new DataCallBack(callQue);

        private Dictionary<object, int> InkCanvasStrokesIndex = new Dictionary<object, int>();
        private int inkCanvasStrokesIndex = 0;
        
        public InkCanvasEx()
        {
            this.InitializeComponent();
            Loaded += InkCanvasControl_Loaded;
        }
        //loaded
        void InkCanvasControl_Loaded(object sender, RoutedEventArgs e)
        {   
            this.thread = new Thread(new ThreadStart(this._startStroke));           
            thread.IsBackground = true;
            this.thread.Start();
        }


        /// <summary>
        /// TouchDown
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            try
            {
                this.Inkcanvas.EditingMode = InkCanvasEditingMode.None;
                Point tp = e.GetTouchPoint(this.Inkcanvas).Position;//get point     
                #region  Add touch point
                if (TouchInScreen.ContainsKey(e.TouchDevice))
                {
                    TouchInScreen.Remove(e.TouchDevice);
                }
                TouchInScreen.Add(e.TouchDevice, tp);
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            e.TouchDevice.Capture(this);//捕获Touchdevice
            e.Handled = true;//处理完毕不在触发OntouchDown函数
            base.Focusable = true;
            base.Focus();
            base.Focusable = false;
        }
        /// <summary>
        /// Move
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewTouchMove(TouchEventArgs e)
        {
            try
            {
                Point tp = e.GetTouchPoint(this.Inkcanvas).Position;
                Point origin = TouchInScreen[e.TouchDevice];
                if (TouchInScreen.ContainsKey(e.TouchDevice))
                {
                    if (origin == tp)
                    {
                        if (InkCanvasStrokesIndex.ContainsKey(e.TouchDevice))
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                }
                else
                {
                    e.Handled = true;
                    return;
                }
                TouchInScreen[e.TouchDevice] = tp;
                if (this.Inkcanvas.EditingMode == InkCanvasEditingMode.None)
                {
                    p.device = e.TouchDevice;
                    p.inputPosition = tp;
                    dbcall(ref p);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            e.Handled = true;
        }        
        /// <summary>
        /// TouchUP
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewTouchUp(TouchEventArgs e)
        {
            try
            {
                //Remove TouchDevice
                TouchInScreen.Remove(e.TouchDevice);
                InkCanvasStrokesIndex.Remove(e.TouchDevice);
                _currentStroke.Remove(e.TouchDevice);
                _strokes.Remove(e.TouchDevice);

                if (TouchCount == 0)
                {
                    //clear all
                    this._currentStroke.Clear();
                    this._strokes.Clear();
                    InkCanvasStrokesIndex.Clear();
                    qData.Clear();
                }
                this.Inkcanvas.EditingMode = InkCanvasEditingMode.None;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            e.Handled = true;
        }


        #region Thread 

        /// <summary>
        /// CallBack
        /// </summary>
        /// <param name="test"></param>
        private static void callQue(ref ThreadPoint test)
        {
            lock (lockThis)
            {
                qData.Enqueue(test);
            }
            mEvent.Set();
        }
        /// <summary>
        /// Collect Stroke
        /// </summary>
        private void _startStroke()
        {

            while (m_bRuning)
            {
                mEvent.WaitOne();
                if (m_bRuning)
                {
                    ThreadPoint data;
                    lock (lockThis)
                    {
                        if (qData.Count > 0)
                        {
                            data = qData.Dequeue();
                        }
                        else
                        {
                            continue;
                        }
                    }
                    try
                    {
                        #region add data                   
                        if (this._strokes.ContainsKey(data.device) && this._currentStroke.ContainsKey(data.device) && InkCanvasStrokesIndex.ContainsKey(data.device))
                        {
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (InkCanvasStrokesIndex.ContainsKey(data.device))
                                    {
                                        int index = InkCanvasStrokesIndex[data.device];
                                        Stroke stroke = inkStrokes[index];
                                        if (stroke != null)
                                        {
                                            StylusPointCollection spc = stroke.StylusPoints;
                                            if (spc != null)
                                            {
                                                StylusPoint point = new StylusPoint(data.inputPosition.X, data.inputPosition.Y, 0.5f);
                                                if (!spc.Contains(point) && TouchInScreen.ContainsKey((TouchDevice)data.device))
                                                    spc.Add(point);
                                                Console.WriteLine(point.ToString());
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }));
                        }
                        else
                        {
                            StylusPointCollection stylusPointCollection = new StylusPointCollection();
                            stylusPointCollection.Add(new StylusPoint(data.inputPosition.X, data.inputPosition.Y, 0.5f));
                            if (stylusPointCollection.Count > 0)
                            {
                                Stroke stroke = new Stroke(stylusPointCollection);

                                this.Dispatcher.Invoke(new Action(() =>
                                {
                                    try
                                    {
                                        stroke.DrawingAttributes.Width = this.Inkcanvas.DefaultDrawingAttributes.Width;
                                        stroke.DrawingAttributes.Height = this.Inkcanvas.DefaultDrawingAttributes.Height;
                                        stroke.DrawingAttributes.Color = this.Inkcanvas.DefaultDrawingAttributes.Color;

                                        #region InkCanvasControlViewModel
                                        inkStrokes.Add(stroke);
                                        InkCanvasStrokes = inkStrokes;
                                        inkCanvasStrokesIndex = inkStrokes.Count - 1;
                                        if (InkCanvasStrokesIndex.ContainsKey(data.device))
                                        {
                                            InkCanvasStrokesIndex.Remove(data.device);
                                        }
                                        InkCanvasStrokesIndex.Add(data.device, inkCanvasStrokesIndex);
                                        #endregion
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message); ;
                                    }
                                }));
                                if (this._currentStroke.ContainsKey(data.device))
                                {
                                    this._currentStroke.Remove(data.device);
                                }
                                this._currentStroke.Add(data.device, stroke);
                                if (this._strokes.ContainsKey(data.device))
                                {
                                    this._strokes[data.device].Add(this._currentStroke[data.device]);
                                    return;
                                }
                                this._strokes.Add(data.device, new StrokeCollection { this._currentStroke[data.device] });
                            }
                        }
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

        }
        #endregion      
    }
}