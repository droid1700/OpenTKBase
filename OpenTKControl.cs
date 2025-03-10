using System.Windows;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using System;
using System.Windows.Markup;


namespace OpenTKBase
{
	public partial class MainWindow : Window
	{
		/// <summary>
		/// 3-Dimensional Point
		/// </summary>
		public struct Point3D
		{
			public double X;
			public double Y;
			public double Z;
		}

		/// <summary>
		/// Object representing a single point on the graph
		/// </summary>
		private class VertexBufferObject
		{
			public Point3D Vertex { get; set; }
			public Color4 Color { get; set; }
			public int VertexNumber { get; set; }
		}

		/// <summary>
		/// Object representing a single point on either axis
		/// </summary>
		private class AxesBufferObject
		{
			public Point3D Vertex { get; set; }
			public Color4 Color { get; set; }
		}

		/// <summary>
		/// Object representing a number label on either axis
		/// </summary>
		private class AxesLabelObject
		{
			public Point3D Vertex { get; set; }
			public Point Texture { get; set; }
			public string Text { get; set; }
		}

		public class Line2D
		{
			public Point p1;
			public Point p2;
		}

		//Buffer arrays
		private float[] xAxisBuffer = new float[0];
		private float[] yAxisBuffer = new float[0];
		private float[] xAxisLabelBuffer = new float[0];
		private float[] yAxisLabelBuffer = new float[0];

		//Buffer data
		private List<VertexBufferObject> vertexData = new List<VertexBufferObject>();
		private List<AxesBufferObject> xAxisRulerBufferData = new List<AxesBufferObject>();
		private List<AxesBufferObject> yAxisRulerBufferData = new List<AxesBufferObject>();
		private List<AxesLabelObject> xAxisLabelBufferData = new List<AxesLabelObject>();
		private List<AxesLabelObject> yAxisLabelBufferData = new List<AxesLabelObject>();

		//Options to make the graph closer to video game graphics where dragging down makes the graph go up
		public bool InvertXAxis = false;
		public bool InvertYAxis = false;

		//private List<Data> previewData = new List<Data>();
		private int vertexBufferPointer, xAxisBufferPointer, yAxisBufferPointer, xAxisLabelBufferPointer, yAxisLabelBufferPointer, texturePointer;
		private Rect dataBounds, xAxisHeader, yAxisHeader;
		public double viewboxScaleFactor, graphScaleFactor;
		public double offsetX, offsetY;
		private Point oldMousePos;
		private int lastHoveredEvent;
		private bool activeHoveredEvent;
		private (bool, Point) moveOriginCrossHair;
		private bool isMousePanning;

		//set values that will be scaled by the resolution
		private Vector graphBounds, xAxisBounds, yAxisBounds, xAxisLabelMargins, yAxisLabelMargins, labelBounds;
		private double headerFontSize, labelFontSize, headerMargin, crossHairSize;
		private double axisMargin, rulerMarkMargin, xAxisLabelOffset;
		private int positiveXTextOffset, negativeXTextOffset, yTextOffset, verticalTextOffset;

		private void GraphControl_Load(object sender, EventArgs e)
		{
			//because the graph needs the scaled size of the graph container from the parent viewbox, this cannot run before the viewbox has finished initializing
			Loaded += delegate
			{
				GraphControl.MakeCurrent();

				//retrieve the scaled size of the graph
				graphBounds = GraphContainer.PointToScreen(new Point(GraphContainer.Width, GraphContainer.Height)) - GraphContainer.PointToScreen(new Point(0, 0));

				//calculate ratio between Width/Height of graph and data
				dataBounds = GetBoundingBox();
				graphScaleFactor = Math.Min(graphBounds.X / dataBounds.Width, graphBounds.Y / dataBounds.Height);

				//bind vertexBuffer data to the graphics memory
				vertexBufferPointer = GL.GenBuffer();
				float[] vertexBuffer = CreateVertexBufferArray(vertexData);
				UpdateBuffer(true, vertexBufferPointer, vertexBuffer);

				offsetX = -Math.Min(dataBounds.Left, 0);
				offsetY = Math.Min(dataBounds.Top, 0);

				//set drawable frame to bounds of GraphContainer
				GL.Viewport(0, 0, (int)graphBounds.X, (int)graphBounds.Y);

				UpdateOrthoView(Math.Max(0.1, graphScaleFactor - 0.1 * graphScaleFactor), offsetX, offsetY);
			};
		}

		/// <summary>
		/// Stores all vertices from the axis buffers into the graphic memory to display on the graph because the axes need the scaled size of their respective containers from the parent viewbox, this cannot run before the viewbox has finished initializing
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void AxisControl_Load(object sender, EventArgs e)
		{
			Loaded += delegate
			{
				CalculateMargins();

				//bind label texture resource to the graphics memory
				texturePointer = GL.GenTexture();
				GL.BindTexture(TextureTarget.Texture2D, texturePointer);

				//set texture to use nearest pixel for color selection and disable texture stretching to fit quad
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)TextureWrapMode.ClampToBorder);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)TextureWrapMode.ClampToBorder);

				//*************************************************************************************
				// initialize scaled size, bounds, axis ruler data, axis label data, view for each axis
				//*************************************************************************************

				//retrieve the scaled size of the x axis
				xAxisBounds = XAxisContainer.PointToScreen(new Point(XAxisContainer.Width, XAxisContainer.Height)) - XAxisContainer.PointToScreen(new Point(0, 0));

				//set bounds for the x axis header
				xAxisHeader = new Rect(xAxisBounds.X - headerMargin, 0, headerMargin, xAxisBounds.Y);

				//bind x axis ruler data to graphics memory
				XAxisControl.MakeCurrent();
				xAxisBufferPointer = GL.GenBuffer();
				UpdateBuffer(true, xAxisBufferPointer, xAxisBuffer);

				//bind x axis label data to graphics memory
				xAxisLabelBufferPointer = GL.GenBuffer();
				UpdateBuffer(true, xAxisLabelBufferPointer, xAxisLabelBuffer);
				GL.BindTexture(TextureTarget.Texture2D, 0);

				//set view for x axis control
				GL.Viewport(0, 0, (int)xAxisBounds.X, (int)xAxisBounds.Y);
				GL.MatrixMode(MatrixMode.Projection);
				GL.LoadIdentity();
				GL.Ortho(0, xAxisBounds.X, 0, xAxisBounds.Y, -1, 1);

				//retrieve the scaled size of the y axis
				yAxisBounds = YAxisContainer.PointToScreen(new Point(YAxisContainer.Width, YAxisContainer.Height)) - YAxisContainer.PointToScreen(new Point(0, 0));

				//set bounds for the y axis header
				yAxisHeader = new Rect(0, yAxisBounds.Y - headerMargin, yAxisBounds.X, headerMargin);

				//bind y axis ruler data to graphics memory
				YAxisControl.MakeCurrent();
				yAxisBufferPointer = GL.GenBuffer();
				UpdateBuffer(true, yAxisBufferPointer, yAxisBuffer);

				//bind y axis label data to graphics memory
				yAxisLabelBufferPointer = GL.GenBuffer();
				UpdateBuffer(true, yAxisLabelBufferPointer, yAxisLabelBuffer);

				//set view for y axis control
				GL.Viewport(0, 0, (int)yAxisBounds.X, (int)yAxisBounds.Y);
				GL.MatrixMode(MatrixMode.Projection);
				GL.LoadIdentity();
				GL.Ortho(0, yAxisBounds.X, 0, yAxisBounds.Y, -1, 1);
			};
		}

		/// <summary>
		/// Displays all ruler buffer data from graphics memory to the rulers
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void AxisControl_Paint(object sender, EventArgs e)
		{
			//********************************************
			// handles the refresh functions of the x axis
			//********************************************

			//switch focus to x axis, clear graph, and set background color
			XAxisControl.MakeCurrent();
			GL.Clear(ClearBufferMask.ColorBufferBit);
			GL.ClearColor(Color4.Black);

			//draw the x axis
			DrawAxisLine(new Line2D() { p1 = new Point(0, xAxisBounds.Y - axisMargin), p2 = new Point(xAxisBounds.X, xAxisBounds.Y - axisMargin) });

			//don't display x axis marks if no data is loaded
			if (true)
			{
				//load x axis ruler color data from graphics memory
				GL.BindBuffer(BufferTarget.ArrayBuffer, xAxisBufferPointer);
				GL.EnableClientState(ArrayCap.ColorArray);
				GL.ColorPointer(4, ColorPointerType.Float, sizeof(float) * 7, sizeof(float) * 3);

				//load x axis ruler vertex data from graphics memory
				GL.EnableClientState(ArrayCap.VertexArray);
				GL.VertexPointer(3, VertexPointerType.Float, sizeof(float) * 7, 0);
				GL.DrawArrays(PrimitiveType.Lines, 0, xAxisBuffer.Length / 7);

				//set a static color value so that the header will not affect the labels
				GL.Color4(Color4.White);

				//load texture buffer from graphics memory
				GL.Enable(EnableCap.Texture2D);
				GL.BindTexture(TextureTarget.Texture2D, texturePointer);

				//load buffer data from graphics memory and assign the last two float values of each vertex as the texture coordinates
				GL.BindBuffer(BufferTarget.ArrayBuffer, xAxisLabelBufferPointer);
				GL.EnableClientState(ArrayCap.TextureCoordArray);
				GL.TexCoordPointer(2, TexCoordPointerType.Float, sizeof(float) * 5, sizeof(float) * 3);

				//assign the first three float values of each vertex as the vertex coordinates
				GL.EnableClientState(ArrayCap.VertexArray);
				GL.VertexPointer(3, VertexPointerType.Float, sizeof(float) * 5, 0);

				//draw the axis labels as quads before applying the texture and refreshing this control
				DrawAxisLabels(true, xAxisLabelBufferData);
				GL.Disable(EnableCap.Texture2D);
			}

			//draw the x axis header
			GL.Enable(EnableCap.Texture2D);
			DrawAxisHeader(true, xAxisHeader);
			GL.Disable(EnableCap.Texture2D);

			XAxisControl.SwapBuffers();

			//********************************************
			// handles the refresh functions of the y axis
			//********************************************

			//switch focus to y axis, clear graph, and set background color
			YAxisControl.MakeCurrent();
			GL.Clear(ClearBufferMask.ColorBufferBit);
			GL.ClearColor(Color4.Black);

			//draw the y axis
			DrawAxisLine(new Line2D() { p1 = new Point(yAxisBounds.X - axisMargin, 0), p2 = new Point(yAxisBounds.X - axisMargin, yAxisBounds.Y) });

			//don't display y axis marks if no data is loaded
			if (true)
			{
				//load y axis ruler color data from graphics memory
				GL.BindBuffer(BufferTarget.ArrayBuffer, yAxisBufferPointer);
				GL.EnableClientState(ArrayCap.ColorArray);
				GL.ColorPointer(4, ColorPointerType.Float, sizeof(float) * 7, sizeof(float) * 3);

				//load y axis ruler vertex data from graphics memory
				GL.EnableClientState(ArrayCap.VertexArray);
				GL.VertexPointer(3, VertexPointerType.Float, sizeof(float) * 7, 0);
				GL.DrawArrays(PrimitiveType.Lines, 0, yAxisBuffer.Length / 7);

				//set a static color value so that the header will not affect the labels
				GL.Color4(Color4.White);

				//load texture buffer from graphics memory
				GL.Enable(EnableCap.Texture2D);
				GL.BindTexture(TextureTarget.Texture2D, texturePointer);

				//load buffer data from graphics memory and assign the last two float values of each vertex as the texture coordinates
				GL.BindBuffer(BufferTarget.ArrayBuffer, yAxisLabelBufferPointer);
				GL.EnableClientState(ArrayCap.TextureCoordArray);
				GL.TexCoordPointer(2, TexCoordPointerType.Float, sizeof(float) * 5, sizeof(float) * 3);

				//assign the first three float values of each vertex as the vertex coordinates
				GL.EnableClientState(ArrayCap.VertexArray);
				GL.VertexPointer(3, VertexPointerType.Float, sizeof(float) * 5, 0);

				//draw the axis labels as quads before applying the texture and refreshing this control
				DrawAxisLabels(false, yAxisLabelBufferData);
				GL.Disable(EnableCap.Texture2D);
			}

			//draw the y axis header
			GL.Enable(EnableCap.Texture2D);
			DrawAxisHeader(false, yAxisHeader);
			GL.Disable(EnableCap.Texture2D);

			YAxisControl.SwapBuffers();

			//set the graph as the active control so that all functions will work properly
			GraphControl.MakeCurrent();
		}

		/// <summary>
		/// Handle mouse move events for AxisControl
		/// TODO: Figure how hovering over vertices
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void AxisControl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			//if hovered event is not a highlighted event, revert it back to it's original color
			//otherwise set the color back to VertexHighlightColor
			//int lastIndex = Context.DataSet.ToList().FindIndex(obj => obj.EventNumber == lastHoveredEvent);
			//UpdateEventColors(!selectedData.Contains(lastIndex), lastHoveredEvent, setupMenu.VertexHighlightColor);
			lastHoveredEvent = 0;
			activeHoveredEvent = false;

			//pass updated values to the graphics memory
			GraphControl.Refresh();
		}


		/// <summary>
		/// Handle mouse move events for GraphControl
		/// TODO: Figure how hovering over vertices
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void GraphControl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			//***********************************************
			//handle mouse dragging in order to pan the graph
			//***********************************************
			if (e.Button == System.Windows.Forms.MouseButtons.Left)
			{
				//retrive current mouse position
				Point mousePos = new Point(e.X, e.Y);

				//only change offset values if mouse is moving while left mouse button is held
				if (isMousePanning)
				{
					//calculate change in mouse position using old mouse position
					double x = mousePos.X - oldMousePos.X;
					double y = mousePos.Y - oldMousePos.Y;

					//increase/decrease offset values based on the direction the mouse is currently moving
					if (InvertXAxis)
					{
						offsetX -= x / graphScaleFactor;
					}
					else
					{
						offsetX += x / graphScaleFactor;
					}

					if (InvertYAxis)
					{
						offsetY -= y / graphScaleFactor;
					}
					else
					{
						offsetY += y / graphScaleFactor;
					}

					//redraw the graph
					UpdateOrthoView(graphScaleFactor, offsetX, offsetY);
				}

				//continue to change offsets if mouse is moving
				oldMousePos = mousePos;
				isMousePanning = true;
			}

			//**************************************************
			//handle the mouse hovering over events on the graph
			//**************************************************
			else
			{
				/*
				List<Data> data = Context.DataSet.ToList();

				//identify whether mouse is touching an event
				bool onEvent = IsMouseOnEvent(System.Windows.Forms.Cursor.Position);

				//convert mouse coordinates to graph coordinates
				Point graphCoords = RevertPoint(new Point(e.X, e.Y));

				//mouse is hovering on event
				if (onEvent)
				{
					//retrieve event number at mouse pointer
					//if data is being modified, use previewData instead
					int eventNumber = Geometry.FindClosestPointToPart(redrawGraph ? previewData : data, graphCoords, Keyboard.IsKeyDown(Key.LeftShift)).Item2;

					//mouse is hovering on new event
					if (eventNumber != lastHoveredEvent)
					{
						//if hovered event is not a highlighted event, revert it back to it's original color
						//otherwise, set the color back to VertexHoverColor
						int lastIndex = data.FindIndex(obj => obj.EventNumber == lastHoveredEvent);
						UpdateEventColors(!selectedData.Contains(lastIndex), lastHoveredEvent, setupMenu.VertexHighlightColor);

						//set active hovered event color to GraphHoverColor and update hover event variables
						int eventIndex = data.FindIndex(obj => obj.EventNumber == eventNumber);
						UpdateEventColors(false, eventNumber, setupMenu.VertexHoverColor);
						lastHoveredEvent = eventNumber;
						activeHoveredEvent = true;
					}
				}
				//mouse is not hovering on event
				else if (activeHoveredEvent)
				{
					//if hovered event is not a highlighted event, revert it back to it's original color
					//otherwise set the color back to VertexHighlightColor
					int lastIndex = data.FindIndex(obj => obj.EventNumber == lastHoveredEvent);
					UpdateEventColors(!selectedData.Contains(lastIndex), lastHoveredEvent, setupMenu.VertexHighlightColor);
					lastHoveredEvent = 0;
					activeHoveredEvent = false;
				}
				*/

				//pass updated values to the graphics memory
				GraphControl.Refresh();

				//don't change offsets if mouse isn't moving
				isMousePanning = false;
			}
		}


		/// <summary>
		/// Handle a mouse wheel event for GraphControl
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void GraphControl_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			//increase or decrease the ratio based on whether scroll wheel is scrolled up or down respectively
			if (e.Delta > 0)
			{
				UpdateOrthoView(Math.Min(9000, graphScaleFactor + 0.1 * graphScaleFactor), offsetX, offsetY);
			}
			else
			{
				UpdateOrthoView(Math.Max(0.1, graphScaleFactor - 0.1 * graphScaleFactor), offsetX, offsetY);
			}
		}

		/// <summary>
		/// Displays all vertex buffer data from graphics memory to the graph
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void GraphControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
		{
			GraphControl.MakeCurrent();

			//clear graph and set background color
			GL.Clear(ClearBufferMask.ColorBufferBit);
			GL.ClearColor(Color4.Black);

			//change line width to make events easier to click
			GL.LineWidth(1);

			//enable transparency
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc((BlendingFactor)BlendingFactorSrc.SrcAlpha, (BlendingFactor)BlendingFactorDest.OneMinusSrcAlpha);

			//bind color data from the buffer to the graph
			GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferPointer);
			GL.EnableClientState(ArrayCap.ColorArray);
			GL.ColorPointer(4, ColorPointerType.Float, sizeof(float) * 7, sizeof(float) * 3);

			//load vertex data from the buffer
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.VertexPointer(3, VertexPointerType.Float, sizeof(float) * 7, 0);
			GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexData.Count);

			//disable transparency
			GL.Disable(EnableCap.Blend);

			//display loaded buffers on the graph
			GraphControl.SwapBuffers();
		}

		/// <summary>
		/// Handle mouse click events for GraphControl
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void GraphControl_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			switch (e.Button)
			{
				case System.Windows.Forms.MouseButtons.Left:
					break;
				case System.Windows.Forms.MouseButtons.Middle:
					break;
				case System.Windows.Forms.MouseButtons.Right:
					// Reset graph view to default location
					// Calculate ratio between Width/Height of graph and new data
					dataBounds = GetBoundingBox();
					graphScaleFactor = Math.Min(graphBounds.X / dataBounds.Width, graphBounds.Y / dataBounds.Height);
					ResetOrthoView(true);
					break;
				default:
					break;
			}

		}

		/// <summary>
		/// Handle mouse double click events for GraphControl
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void GraphControl_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
		{
		}

		private Rect GetBoundingBox()
		{
			Point minPoint = new Point(double.MaxValue, double.MaxValue);
			Point maxPoint = new Point(double.MinValue, double.MinValue);

			for (int i = 0; i < vertexData.Count; i++)
			{
				minPoint.X = Math.Min(minPoint.X, vertexData[i].Vertex.X);
				minPoint.Y = Math.Min(minPoint.Y, vertexData[i].Vertex.Y);
				maxPoint.X = Math.Max(maxPoint.X, vertexData[i].Vertex.X);
				maxPoint.Y = Math.Max(maxPoint.Y, vertexData[i].Vertex.Y);
			}

			return new Rect(minPoint, maxPoint);
		}

		/// <summary>
		/// Create a float array that represents all vertex data for the graph
		/// </summary>
		/// <param name="vertexData"></param>
		/// <returns></returns>
		private float[] CreateVertexBufferArray(List<VertexBufferObject> vertexData)
		{
			//each VertexBufferObject has three floats representing X, Y, and Z coordinates and 4 floats representing
			//R, G, B, and A color values, so the size of the float[] will be 7 times larger than the list of objects
			float[] vertexBuffer = new float[vertexData.Count * 7];

			//since every vertex is represented by 7 different floats, add the vertex values
			//from vertexData to each group of 7 floats in vertexBuffer
			for (int i = 0; i < vertexBuffer.Length; i += 7)
			{
				vertexBuffer[i] = (float)vertexData[i / 7].Vertex.X; //X coordinate
				vertexBuffer[i + 1] = (float)vertexData[i / 7].Vertex.Y; //Y coordinate
				vertexBuffer[i + 2] = (float)vertexData[i / 7].Vertex.Z; //Z coordinate
				vertexBuffer[i + 3] = vertexData[i / 7].Color.R; //R color value
				vertexBuffer[i + 4] = vertexData[i / 7].Color.G; //G color value
				vertexBuffer[i + 5] = vertexData[i / 7].Color.B; //B color value
				vertexBuffer[i + 6] = vertexData[i / 7].Color.A; //A color value
			}

			return vertexBuffer;
		}

		/// <summary>
		/// Update the buffer stored in the graphics memory with new data
		/// </summary>
		/// <param name="sizeChanged">Whether or not the size of the buffer has changed</param>
		/// <param name="pointer">The pointer of the buffer</param>
		/// <param name="buffer">The float[] storing vertex data</param>
		private void UpdateBuffer(bool sizeChanged, int pointer, float[] buffer)
		{
			//load buffer from graphics memory
			GL.BindBuffer(BufferTarget.ArrayBuffer, pointer);

			//if the size of the buffer has changed, then GL.BufferData must be called, otherwise use GL.BufferSubData
			if (sizeChanged)
			{
				GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * buffer.Length, buffer, BufferUsageHint.DynamicDraw);
			}
			else
			{
				GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeof(float) * buffer.Length, buffer);
			}

			//unload buffer from graphics memory
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
		}

		/// <summary>
		/// Add ruler marks to axis buffers based on graphScaleFactor and offset values
		/// </summary>
		private void AddRulerMarks()
		{
			//clear the label
			xAxisLabelBufferData.Clear();
			yAxisLabelBufferData.Clear();

			//find bottom left and top right points on graph
			Point lowerBounds = RevertPoint(new Point(0, graphBounds.Y));
			Point upperBounds = RevertPoint(new Point(graphBounds.X, 0));

			//calculate step of rulers by rounding the distance between the lower and upper bounds to the highest non-zero number
			double step = RoundToHighestPlaceValue(upperBounds.X - lowerBounds.X);

			//*************************************************************
			//add the x axis ruler marks and labels to the buffer data list
			//*************************************************************

			//find the lowest and highest value rounded by the step value
			//add an extra value on either side so the graph always shows a full axis
			double floor = Math.Round((lowerBounds.X - step) / step) * step;
			double ceiling = Math.Round((upperBounds.X + step) / step) * step;

			//draw ruler marks for each whole number between lower and upper bounds on X Axis
			for (double i = floor; i < ceiling; i += step)
			{
				//string markValue = (Math.Round(i * 10d) / 10).ToString();
				string markValue = Math.Round(i, 2).ToString();

				//calculate x value for ruler mark
				double xMark = TransformPoint(new Point(i, 0)).X;

				//add ruler mark start point
				xAxisRulerBufferData.Add(new AxesBufferObject()
				{
					Vertex = new Point3D() { X = xMark, Y = xAxisBounds.Y, Z = 0 },
					Color = Color4.White
				});

				//add ruler mark end point
				xAxisRulerBufferData.Add(new AxesBufferObject()
				{
					Vertex = new Point3D() { X = xMark, Y = xAxisBounds.Y - rulerMarkMargin, Z = 0 },
					Color = Color4.White
				});

				//add label corners
				xAxisLabelBufferData.AddRange(new List<AxesLabelObject>
				{
                    //add bottom left corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = xMark - xAxisLabelMargins.X, Y = xAxisLabelMargins.Y - xAxisLabelOffset, Z = 0 },
						Texture = new Point(0, 1),
						Text = markValue
					},
                    //add top left corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = xMark - xAxisLabelMargins.X, Y = xAxisLabelMargins.Y, Z = 0 },
						Texture = new Point(0, 0),
						Text = markValue
					},
                    //add top right corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = xMark + xAxisLabelMargins.X, Y = xAxisLabelMargins.Y, Z = 0 },
						Texture = new Point(1, 0),
						Text = markValue
					},
                    //add bottom right corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = xMark + xAxisLabelMargins.X, Y = xAxisLabelMargins.Y - xAxisLabelOffset, Z = 0 },
						Texture = new Point(1, 1),
						Text = markValue
					}
				});

				AddMinorRulerMarks(true, step, i);
			}

			//*************************************************************
			//add the y axis ruler marks and labels to the buffer data list
			//*************************************************************

			//find the lowest and highest value rounded by the step value
			//add an extra value on either side so the graph always shows a full axis
			floor = Math.Round((lowerBounds.Y - step) / step) * step;
			ceiling = Math.Round((upperBounds.Y + step) / step) * step;

			//draw ruler marks for each whole number between lower and upper bounds on Y Axis
			for (double i = floor; i < ceiling; i += step)
			{
				string markValue = Math.Round(i, 2).ToString();

				//calculate y value for ruler mark
				double yMark = TransformPoint(new Point(0, i)).Y;

				//add ruler mark start point
				yAxisRulerBufferData.Add(new AxesBufferObject()
				{
					Vertex = new Point3D() { X = yAxisBounds.X, Y = yAxisBounds.Y - yMark, Z = 0 },
					Color = Color4.White
				});

				//add ruler mark end point
				yAxisRulerBufferData.Add(new AxesBufferObject()
				{
					Vertex = new Point3D() { X = yAxisBounds.X - rulerMarkMargin, Y = yAxisBounds.Y - yMark, Z = 0 },
					Color = Color4.White
				});

				//add label corners
				yAxisLabelBufferData.AddRange(new List<AxesLabelObject>
				{
                    //add bottom left corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = 0, Y = yAxisBounds.Y - yMark - yAxisLabelMargins.Y, Z = 0 },
						Texture = new Point(0, 1),
						Text = markValue
					},
                    //add top left corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = 0, Y = yAxisBounds.Y - yMark + yAxisLabelMargins.Y, Z = 0 },
						Texture = new Point(0, 0),
						Text = markValue
					},
                    //add top right corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = yAxisBounds.X - yAxisLabelMargins.X, Y = yAxisBounds.Y - yMark + yAxisLabelMargins.Y, Z = 0 },
						Texture = new Point(1, 0),
						Text = markValue
					},
                    //add bottom right corner of label
                    new AxesLabelObject()
					{
						Vertex = new Point3D() { X = yAxisBounds.X - yAxisLabelMargins.X, Y = yAxisBounds.Y - yMark - yAxisLabelMargins.Y, Z = 0 },
						Texture = new Point(1, 1),
						Text = markValue
					}
				});

				AddMinorRulerMarks(false, step, i);
			}

			//******************************************************
			//load the x and y axis data onto the respective buffers
			//******************************************************

			//add ruler marks to x axis buffer
			Array.Resize(ref xAxisBuffer, xAxisRulerBufferData.Count * 7);
			for (int i = 0; i < xAxisRulerBufferData.Count; i++)
			{
				xAxisBuffer[i * 7] = (float)xAxisRulerBufferData[i].Vertex.X;
				xAxisBuffer[i * 7 + 1] = (float)xAxisRulerBufferData[i].Vertex.Y;
				xAxisBuffer[i * 7 + 2] = (float)xAxisRulerBufferData[i].Vertex.Z;
				xAxisBuffer[i * 7 + 3] = xAxisRulerBufferData[i].Color.R;
				xAxisBuffer[i * 7 + 4] = xAxisRulerBufferData[i].Color.G;
				xAxisBuffer[i * 7 + 5] = xAxisRulerBufferData[i].Color.B;
				xAxisBuffer[i * 7 + 6] = xAxisRulerBufferData[i].Color.A;
			}

			//add ruler labels to x axis buffer
			Array.Resize(ref xAxisLabelBuffer, xAxisLabelBufferData.Count * 5);
			for (int i = 0; i < xAxisLabelBufferData.Count; i++)
			{
				xAxisLabelBuffer[i * 5] = (float)xAxisLabelBufferData[i].Vertex.X;
				xAxisLabelBuffer[i * 5 + 1] = (float)xAxisLabelBufferData[i].Vertex.Y;
				xAxisLabelBuffer[i * 5 + 2] = (float)xAxisLabelBufferData[i].Vertex.Z;
				xAxisLabelBuffer[i * 5 + 3] = (float)xAxisLabelBufferData[i].Texture.X;
				xAxisLabelBuffer[i * 5 + 4] = (float)xAxisLabelBufferData[i].Texture.Y;
			}

			//add ruler marks to y axis buffer
			Array.Resize(ref yAxisBuffer, yAxisRulerBufferData.Count * 7);
			for (int i = 0; i < yAxisRulerBufferData.Count; i++)
			{
				yAxisBuffer[i * 7] = (float)yAxisRulerBufferData[i].Vertex.X;
				yAxisBuffer[i * 7 + 1] = (float)yAxisRulerBufferData[i].Vertex.Y;
				yAxisBuffer[i * 7 + 2] = (float)yAxisRulerBufferData[i].Vertex.Z;
				yAxisBuffer[i * 7 + 3] = yAxisRulerBufferData[i].Color.R;
				yAxisBuffer[i * 7 + 4] = yAxisRulerBufferData[i].Color.G;
				yAxisBuffer[i * 7 + 5] = yAxisRulerBufferData[i].Color.B;
				yAxisBuffer[i * 7 + 6] = yAxisRulerBufferData[i].Color.A;
			}

			//add ruler labels to y axis buffer
			Array.Resize(ref yAxisLabelBuffer, yAxisLabelBufferData.Count * 5);
			for (int i = 0; i < yAxisLabelBufferData.Count; i++)
			{
				yAxisLabelBuffer[i * 5] = (float)yAxisLabelBufferData[i].Vertex.X;
				yAxisLabelBuffer[i * 5 + 1] = (float)yAxisLabelBufferData[i].Vertex.Y;
				yAxisLabelBuffer[i * 5 + 2] = (float)yAxisLabelBufferData[i].Vertex.Z;
				yAxisLabelBuffer[i * 5 + 3] = (float)yAxisLabelBufferData[i].Texture.X;
				yAxisLabelBuffer[i * 5 + 4] = (float)yAxisLabelBufferData[i].Texture.Y;
			}

			//modify axis vertex data saved to graphics memory for both x and y axis
			UpdateBuffer(true, xAxisBufferPointer, xAxisBuffer);
			UpdateBuffer(true, xAxisLabelBufferPointer, xAxisLabelBuffer);
			UpdateBuffer(true, yAxisBufferPointer, yAxisBuffer);
			UpdateBuffer(true, yAxisLabelBufferPointer, yAxisLabelBuffer);
		}

		/// <summary>
		/// Convert mouse pixel position to graph coordinates
		/// </summary>
		/// <param name="mouseCoords"></param>
		/// <returns></returns>
		private Point RevertPoint(Point mouseCoords)
		{
			//alter the mouseCoords using graphScaleFactor and offset values
			Vector4 graphCoords = new Vector4((float)(mouseCoords.X / graphScaleFactor - offsetX), (float)((graphBounds.Y - mouseCoords.Y) / graphScaleFactor + offsetY), 0.0f, 1.0f);

			//load mouse coordinates into a Vector4 and then apply the inverse Modelview matrix to the vector
			GL.GetFloat(GetPName.ModelviewMatrix, out Matrix4 modelMatrix);
			graphCoords *= modelMatrix.Inverted();
			return new Point(graphCoords.X, graphCoords.Y);
		}

		/// <summary>
		/// Round the value to the highest non-zero place value
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		private double RoundToHighestPlaceValue(double i)
		{
			double digits = Math.Floor(Math.Log10(i));
			double unit = Math.Pow(10, digits);
			return Math.Ceiling(i / unit) * unit / 10;
		}

		/// <summary>
		/// Convert graph coordinates to pixel coordinates
		/// </summary>
		/// <param name="graphPos"></param>
		/// <returns></returns>
		private Point TransformPoint(Point graphPos)
		{
			//load mouse coordinates into a Vector4 and then apply the Modelview matrix to the vector
			Vector4 pixelCoords = new Vector4((float)graphPos.X, (float)graphPos.Y, 0.0f, 1.0f);
			GL.GetFloat(GetPName.ModelviewMatrix, out Matrix4 modelMatrix);
			pixelCoords *= modelMatrix;

			//invert the Vector4 using graphScaleFactor and offset values
			pixelCoords.X = (float)Math.Round(graphScaleFactor * (pixelCoords.X + offsetX));
			pixelCoords.Y = (float)Math.Round(-(graphScaleFactor * (pixelCoords.Y - offsetY) - graphBounds.Y));
			return new Point(pixelCoords.X, pixelCoords.Y);
		}

		/// <summary>
		/// Add smaller ruler marks to axis buffers based on graphScaleFactor and offset values
		/// </summary>
		/// <param name="isOnX"></param>
		/// <param name="step"></param>
		/// <param name="mark"></param>
		private void AddMinorRulerMarks(bool isOnX, double step, double mark)
		{
			//divide step by 10 to get a lower step value for smaller marks
			step /= 10;
			int minorMarkMargin = (int)Math.Round(5 * viewboxScaleFactor);
			int midMarkMargin = (int)Math.Round(2.5 * viewboxScaleFactor);

			for (int i = 1; i < 10; i++)
			{
				//set mark offset to a lower value on the middle mark to make it longer than the rest
				double markOffset = i == 5 ? midMarkMargin : minorMarkMargin;

				if (isOnX)
				{
					double xMark = TransformPoint(new Point(mark + (step * i), 0)).X;

					//add ruler mark start point
					xAxisRulerBufferData.Add(new AxesBufferObject()
					{
						Vertex = new Point3D() { X = xMark, Y = xAxisBounds.Y - markOffset, Z = 0 },
						Color = Color4.White
					});

					//add ruler mark end point
					xAxisRulerBufferData.Add(new AxesBufferObject()
					{
						Vertex = new Point3D() { X = xMark, Y = xAxisBounds.Y - rulerMarkMargin + markOffset, Z = 0 },
						Color = Color4.White
					});
				}
				else
				{
					double yMark = TransformPoint(new Point(0, mark + (step * i))).Y;

					//add ruler mark start point
					yAxisRulerBufferData.Add(new AxesBufferObject()
					{
						Vertex = new Point3D() { X = yAxisBounds.X - markOffset, Y = yAxisBounds.Y - yMark, Z = 0 },
						Color = Color4.White
					});

					//add ruler mark end point
					yAxisRulerBufferData.Add(new AxesBufferObject()
					{
						Vertex = new Point3D() { X = yAxisBounds.X - rulerMarkMargin + markOffset, Y = yAxisBounds.Y - yMark, Z = 0 },
						Color = Color4.White
					});
				}
			}
		}

		/// <summary>
		/// Calculates the scaled values for all margins and offsets used by the axes
		/// </summary>
		private void CalculateMargins()
		{
			//calculate the scale factor that is being applied to the graph by multiplying the MasterViewBox scale factor by the GraphView scale factor
			//This calculation determines the cumulative scaling factor applied to a nested element due to multiple levels of ViewBox transformations. It is computed as the product of the scaling factor from the outer ViewBox to its child and the scaling factor from the inner ViewBox to its child. This ensures the final element's size accounts for both scaling layers.
			//FrameworkElement masterChild = MasterViewBox.Child as FrameworkElement;
			//FrameworkElement graphChild = GraphView.Child as FrameworkElement;
			//viewboxScaleFactor = MasterViewBox.ActualWidth / masterChild.ActualWidth * (GraphView.ActualWidth / graphChild.ActualWidth);
			viewboxScaleFactor = 1;

			//********************************************************************
			//scale all margins and offset values for the axes by the scale factor
			//********************************************************************
			headerFontSize = 24 * viewboxScaleFactor;
			labelFontSize = 16 * Math.Sqrt(viewboxScaleFactor);
			//width of xAxisHeader and height of yAxisHeader
			headerMargin = 60 * viewboxScaleFactor;
			//half of the length of the cross hair
			crossHairSize = double.MaxValue;
			//distance to move away from max height on the x axis and max width on the y axis
			axisMargin = 10 * viewboxScaleFactor;
			//length of each ruler mark
			rulerMarkMargin = 20 * viewboxScaleFactor;
			//the offset of the top and bottom of x axis label quad
			xAxisLabelOffset = 16 * viewboxScaleFactor;
			//height and width of x axis label quad
			xAxisLabelMargins = new Vector(32 * viewboxScaleFactor, 32 * viewboxScaleFactor);
			//height and width of y axis label quad
			yAxisLabelMargins = new Vector(20 * viewboxScaleFactor, 7.5 * viewboxScaleFactor);
			//width and height of each axis label
			labelBounds = new Vector(Math.Round(64 * viewboxScaleFactor), Math.Round(16 * viewboxScaleFactor));
			//x offset for positive text values along the x axis
			positiveXTextOffset = (int)Math.Round(32 * viewboxScaleFactor);
			//x offset for negative text values along the x axis
			negativeXTextOffset = (int)Math.Round(28 * viewboxScaleFactor);
			//x offset for text values along the y axis
			yTextOffset = (int)Math.Round(54 * viewboxScaleFactor);
			//y offset for text values along both axes
			verticalTextOffset = (int)Math.Round(8 * viewboxScaleFactor);
		}

		/// <summary>
		/// Draws the axes
		/// </summary>
		/// <param name="axis"></param>
		private void DrawAxisLine(Line2D axis)
		{
			GL.Color4(Color4.White);

			GL.Begin(PrimitiveType.Lines);
			GL.Vertex3(axis.p1.X, axis.p1.Y, 0);
			GL.Vertex3(axis.p2.X, axis.p2.Y, 0);
			GL.End();
		}

		/// <summary>
		/// Draws the axis label as a quad and applies a bitmap texture showing the value of the label onto the quad
		/// </summary>
		/// <param name="isXAxis"></param>
		/// <param name="labels"></param>
		private void DrawAxisLabels(bool isXAxis, List<AxesLabelObject> labels)
		{
			//horizontally, labels along the x axis are aligned in the center, labels along the y axis are aligned to the right
			//vertically, both are aligned in the center
			System.Drawing.StringFormat format = new System.Drawing.StringFormat
			{
				Alignment = isXAxis ? System.Drawing.StringAlignment.Center : System.Drawing.StringAlignment.Far,
				LineAlignment = System.Drawing.StringAlignment.Center
			};

			for (int i = 0; i < labels.Count; i += 4)
			{
				//NOTE: because the y axis is aligned on the right and the x axis is centered, they need different x locations
				//NOTE: negative x values are centered in relation to just the numbers, not including the negative sign
				int xLoc = isXAxis ? labels[i].Text.Contains("-") ? negativeXTextOffset : positiveXTextOffset : yTextOffset;

				//create the bitmap and lock it so that we can retrieve the starting pixel position for the texture
				System.Drawing.Bitmap bmp = GenerateLabelTexture(format, (int)labelBounds.X, (int)labelBounds.Y, new System.Drawing.Font("Consolas", (float)labelFontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel), labels[i].Text, xLoc, verticalTextOffset);
				System.Drawing.Imaging.BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

				//bind the bitmap to the texture that will be applied to the quad
				GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
				bmp.UnlockBits(data);

				GL.DrawArrays(PrimitiveType.Quads, i, 5);
			}
		}

		/// <summary>
		/// Generate a bitmap of the provided text
		/// </summary>
		/// <param name="format"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="font"></param>
		/// <param name="text"></param>
		/// <param name="xLoc"></param>
		/// <param name="yLoc"></param>
		/// <returns></returns>
		private System.Drawing.Bitmap GenerateLabelTexture(System.Drawing.StringFormat format, int width, int height, System.Drawing.Font font, string text, int xLoc, int yLoc)
		{
			System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(width, height);

			//draw the text onto the bitmap
			using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
			{
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
				g.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, System.Drawing.Color.FromArgb(Convert.ToInt32("000000", 16)))), 0, 0, bmp.Width, bmp.Height);   //draw the background
				g.DrawString(text, font, new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, System.Drawing.Color.FromArgb(Convert.ToInt32("ffffff", 16)))), xLoc, yLoc, format); //draw the text
				g.Flush();

				font.Dispose();
				g.Dispose();
			}

			return bmp;
		}

		/// <summary>
		/// Draws the axis header as a quad and applies a bitmap texture showing the label of the header onto the quad
		/// </summary>
		/// <param name="isXAxis"></param>
		/// <param name="axisHeader"></param>
		private void DrawAxisHeader(bool isXAxis, Rect axisHeader)
		{
			//horizontally, labels along the x axis are aligned in the center, labels along the y axis are aligned to the right
			//vertically, labels along the x axis are aligned to the right, labels along the y axis are aligned in the center
			System.Drawing.StringFormat format = new System.Drawing.StringFormat
			{
				Alignment = isXAxis ? System.Drawing.StringAlignment.Center : System.Drawing.StringAlignment.Far,
				LineAlignment = isXAxis ? System.Drawing.StringAlignment.Far : System.Drawing.StringAlignment.Center
			};

			//x and y locations are set according to the formatting above, allow all text to fit on screen
			int xLoc = isXAxis ? (int)axisHeader.Width / 2 : (int)axisHeader.Width;
			int yLoc = isXAxis ? (int)axisHeader.Height : (int)axisHeader.Height / 2;

			System.Drawing.Bitmap bmp = GenerateLabelTexture(format, (int)axisHeader.Width, (int)axisHeader.Height, new System.Drawing.Font("Arial", (float)headerFontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel), isXAxis ? "X+" : "Y+", xLoc, yLoc);
			System.Drawing.Imaging.BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			//bind the bitmap to the texture that will be applied to the quad
			GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
			bmp.UnlockBits(data);

			//draw the crosshair using the points defined above
			GL.Begin(PrimitiveType.Quads);
			GL.TexCoord2(0, isXAxis ? 1 : 0); GL.Vertex3(axisHeader.Left, axisHeader.Bottom, 0);
			GL.TexCoord2(0, isXAxis ? 0 : 1); GL.Vertex3(axisHeader.Left, axisHeader.Top, 0);
			GL.TexCoord2(1, isXAxis ? 0 : 1); GL.Vertex3(axisHeader.Right, axisHeader.Top, 0);
			GL.TexCoord2(1, isXAxis ? 1 : 0); GL.Vertex3(axisHeader.Right, axisHeader.Bottom, 0);
			GL.End();
		}

		//**********************************************************************************
		//Public methods
		//**********************************************************************************

		/// <summary>
		/// Resizes the orthographic projection to fit the dataset offset and scaled by offsetX/offsetY and graphScaleFactor respectively
		/// </summary>
		/// <param name="ratio"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void UpdateOrthoView(double ratio, double x, double y)
		{
			//translate the expected center point of the graph to (0, 0)
			GL.MatrixMode(MatrixMode.Modelview);
			GL.Translate(-graphBounds.X / (graphScaleFactor * 2), -graphBounds.Y / (graphScaleFactor * 2), 0);
			graphScaleFactor = ratio;

			//scales the graph based on the ratio between graph size and event data
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();
			GL.Ortho(0 - x, graphBounds.X / graphScaleFactor - x, 0 + y, graphBounds.Y / graphScaleFactor + y, -1, 1);

			//translate the expected center point of the graph to center
			GL.MatrixMode(MatrixMode.Modelview);
			GL.Translate(graphBounds.X / (graphScaleFactor * 2), graphBounds.Y / (graphScaleFactor * 2), 0);

			//repaints the graph
			GraphControl.Refresh();

			//clear axes data
			xAxisRulerBufferData.Clear();
			xAxisLabelBufferData.Clear();
			yAxisRulerBufferData.Clear();
			yAxisLabelBufferData.Clear();

			//draw the ruler marks and labels
			AddRulerMarks();

			//repaints the axes
			XAxisControl.Refresh();
		}

		/// <summary>
		/// Resets the orthographic projection to fit the dataset
		/// </summary>
		/// <param name="newData"></param>
		public void ResetOrthoView(bool newData)
		{
			if (newData)
			{
				//translate the expected center point of the graph to (0, 0)
				GL.MatrixMode(MatrixMode.Modelview);
				GL.LoadIdentity();
			}

			//calculate ratio between Width/Height of graph and data
			dataBounds = GetBoundingBox();
			double tempScaleFactor = Math.Min(graphBounds.X / dataBounds.Width, graphBounds.Y / dataBounds.Height);

			//reset offset values and update orthographic view using default values
			offsetX = -Math.Min(dataBounds.Left, 0);
			offsetY = Math.Min(dataBounds.Top, 0);
			UpdateOrthoView(Math.Max(0.1, tempScaleFactor - 0.1 * tempScaleFactor), offsetX, offsetY);
		}

		public void AddVertex(double x, double y, double z, int a, int r, int g, int b)
		{
			Color4 color = System.Drawing.Color.FromArgb(a, r, g, b);
			int vertexNumber = vertexData.Count + 1;
			VertexBufferObject vbo = new VertexBufferObject()
			{
				Vertex = new Point3D() { X = x, Y = y, Z = z },
				Color = color,
				VertexNumber = vertexNumber
			};

			vertexData.Add(vbo);
		}
	}
}
