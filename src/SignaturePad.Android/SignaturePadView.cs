using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Graphics;

namespace SignaturePad {
	public class SignaturePadView : FrameLayout {
		#region UI Controls
		TextView lblSign;
		View signatureLine;
		TextView xLabel;
		TextView lblClear;
		ClearingImageView imageView;
		#endregion

		Context context;
		Paint paint;

		Path currentPath;
		List<Path> paths;
		List<System.Drawing.PointF> currentPoints;
		List<System.Drawing.PointF []> points;

		float lastX;
		float lastY;

		//Used to determine rectangle that needs to be redrawn.
		RectF dirtyRect;

		//Create an array containing all of the points used to draw the signature.  Uses null
		//to indicate a new line.
		public System.Drawing.PointF[] Points {
			get { 
				if (points == null || points.Count () == 0)
					return new System.Drawing.PointF [0];

				List<System.Drawing.PointF> pointsList = points [0].ToList ();
				
				for (var i = 1; i < points.Count; i++) {
					pointsList.Add (System.Drawing.PointF.Empty);
					pointsList = pointsList.Concat (points [i]).ToList ();
				}
				
				return pointsList.ToArray (); 
			}
		}

		public bool IsBlank {
			get { return Points.Count () == 0; }
		}

		Color strokeColor;
		public Color StrokeColor {
			get { return strokeColor; }
			set {
				strokeColor = value;

				if (paint != null)
					paint.Color = strokeColor;

				if (!IsBlank)
					imageView.SetImageBitmap (GetImage(false));
			}
		}

		Color backgroundColor;
		public Color BackgroundColor {
			get { return backgroundColor; }
			set {
				backgroundColor = value;
				SetBackgroundColor (backgroundColor);
			}
		}

		float lineWidth;
		public float LineWidth {
			get { return lineWidth; }
			set {
				lineWidth = value;
				if (paint != null)
					paint.StrokeWidth = lineWidth;

				if (!IsBlank)
					imageView.SetImageBitmap (GetImage (false));
			}
		}

		public SignaturePadView (Context context) : base (context)
		{
			this.context = context;
			Initialize ();
		}

		public SignaturePadView (Context context, IAttributeSet attrs) : base (context, attrs)
		{
			this.context = context;
			Initialize ();
		}

		public SignaturePadView (Context context, IAttributeSet attrs, int defStyle) :
			base (context, attrs, defStyle)
		{
			this.context = context;
			Initialize ();
		}

		void Initialize ()
		{
			BackgroundColor = Color.Black;
			strokeColor = Color.White;
			lineWidth = 2f;

			//Set the attributes for painting the lines on the screen.
			paint = new Paint ();
			paint.Color = strokeColor;
			paint.StrokeWidth = lineWidth;
			paint.SetStyle (Paint.Style.Stroke);
			paint.StrokeJoin = Paint.Join.Round;
			paint.StrokeCap = Paint.Cap.Round;
			paint.AntiAlias = true;

			#region Add Subviews
			//Add an image that covers the entire signature view, used to display already drawn
			//elements instead of having to redraw them every time the user touches the screen.
			imageView = new ClearingImageView (context);
			imageView.SetBackgroundColor (Color.Transparent);
			AddView (imageView);

			lblSign = new TextView (context);
			lblSign.Text = "Sign Here";
			AddView (lblSign);

			//Display the base line for the user to sign on.
			signatureLine = new View (context);
			signatureLine.SetBackgroundColor (Color.Gray);
			AddView (signatureLine);

			//Display the X on the left hand side of the line where the user signs.
			xLabel = new TextView (context);
			xLabel.Text = "X";
			xLabel.SetTypeface (null, TypefaceStyle.Bold);
			AddView (xLabel);

			lblClear = new TextView (context);
			lblClear.Text = "Clear";
			lblClear.Click += (object sender, EventArgs e) => {
				Clear ();
			};
			AddView (lblClear);
			lblClear.Visibility = ViewStates.Invisible;
			#endregion

			paths = new List<Path> ();
			points = new List<System.Drawing.PointF[]> ();
			currentPoints = new List<System.Drawing.PointF> ();

			dirtyRect = new RectF ();
		}

		//Delete the current signature.
		public void Clear ()
		{
			paths = new List<Path> ();
			points = new List<System.Drawing.PointF[]> ();
			currentPoints = new List<System.Drawing.PointF> ();
			currentPath = new Path ();
			imageView.SetImageBitmap (null);
			lblClear.Visibility = ViewStates.Invisible;
			GC.Collect ();
			
			Invalidate ();
		}

		//Create a UIImage of the currently drawn signature.
		public Bitmap GetImage (bool shouldCrop = true)
		{
			return GetImage (strokeColor, Color.Transparent, new System.Drawing.SizeF (Width, Height), 1,
			                 shouldCrop);
		}

		public Bitmap GetImage (System.Drawing.SizeF size, bool shouldCrop = true)
		{
			return GetImage (strokeColor, Color.Transparent, size, 
			                 getScaleFromSize (size, Width, Height), shouldCrop);
		}

		public Bitmap GetImage (float scale, bool shouldCrop = true)
		{
			return GetImage (strokeColor, Color.Transparent, getSizeFromScale (scale, Width, Height), 
			                 scale, shouldCrop);
		}

		//Create a UIImage of the currently drawn signature with the specified Stroke color.
		public Bitmap GetImage (Color strokeColor, bool shouldCrop = true)
		{
			return GetImage (strokeColor, Color.Transparent, new System.Drawing.SizeF (Width, Height), 1,
			                 shouldCrop);
		}

		public Bitmap GetImage (Color strokeColor, System.Drawing.SizeF size, bool shouldCrop = true)
		{
			return GetImage (strokeColor, Color.Transparent, size, getScaleFromSize (size, Width, Height), 
			                 shouldCrop);
		}

		public Bitmap GetImage (Color strokeColor, float scale, bool shouldCrop = true)
		{
			return GetImage (strokeColor, Color.Transparent, getSizeFromScale (scale, Width, Height), 
			                 scale, shouldCrop);
		}

		//Create a UIImage of the currently drawn signature with the specified Stroke and Fill colors.
		public Bitmap GetImage (Color strokeColor, Color fillColor, bool shouldCrop = true)
		{
			return GetImage (strokeColor, fillColor, new System.Drawing.SizeF (Width, Height), 1, 
			                 shouldCrop);
		}

		public Bitmap GetImage (Color strokeColor, Color fillColor, System.Drawing.SizeF size, 
		                        bool shouldCrop = true)
		{
			return GetImage (strokeColor, fillColor, size, getScaleFromSize (size, Width, Height), 
			                 shouldCrop);
		}

		public Bitmap GetImage (Color strokeColor, Color fillColor, float scale, bool shouldCrop = true)
		{
			return GetImage (strokeColor, fillColor, getSizeFromScale (scale, Width, Height), 
			                 scale, shouldCrop);
		}

		Bitmap GetImage (Color strokeColor, Color fillColor, System.Drawing.SizeF size, float scale, 
		                 bool shouldCrop = true)
		{
			if (size.Width == 0 || size.Height == 0 || scale <= 0)
				return null;

			float uncroppedScale;
			System.Drawing.SizeF uncroppedSize;
			RectF croppedRectangle;

			if (shouldCrop && Points.Count () != 0) {
				croppedRectangle = getCroppedRectangle ();
				float scaleX = (croppedRectangle.Right - croppedRectangle.Left) / Width;
				float scaleY = (croppedRectangle.Bottom - croppedRectangle.Top) / Height;
				uncroppedScale = 1 / Math.Max (scaleX, scaleY);
				//uncroppedScale = 1 / getScaleFromSize (croppedRectangle.Size, Bounds);
				uncroppedSize = getSizeFromScale (uncroppedScale, size.Width, size.Height);
			} else {
				uncroppedScale = scale;
				uncroppedSize = size;
			}

			Bitmap image = Bitmap.CreateBitmap ((int)uncroppedSize.Width, (int)uncroppedSize.Height, 
			                                    Bitmap.Config.Argb8888);
			Canvas canvas = new Canvas (image);
			canvas.Scale (uncroppedScale, uncroppedScale);
			paint.Color = strokeColor;
			canvas.DrawColor (fillColor);

			foreach (var path in paths)
				canvas.DrawPath (path, paint);
	
			if (shouldCrop && Points.Count () != 0) {
				croppedRectangle = getCroppedRectangle ();
				RectF scaledRectangle = new RectF (croppedRectangle.Left * uncroppedScale,
				                                   croppedRectangle.Top * uncroppedScale,
				                                   croppedRectangle.Right * uncroppedScale,
				                                   croppedRectangle.Bottom * uncroppedScale);

				if (scaledRectangle.Left >= 5)
					scaledRectangle.Left -= 5;
				if (scaledRectangle.Right <= uncroppedSize.Width - 5)
					scaledRectangle.Right += 5;
				if (scaledRectangle.Top >= 5)
					scaledRectangle.Top -= 5;
				if (scaledRectangle.Bottom <= uncroppedSize.Height - 5)
					scaledRectangle.Bottom += 5;
				image = Bitmap.CreateBitmap (image, 
				                             (int) scaledRectangle.Left, 
				                             (int) scaledRectangle.Top,
				                             (int) scaledRectangle.Width (),
				                             (int) scaledRectangle.Height ());
			}
			paint.Color = this.strokeColor;
			return image;
		}

		RectF getCroppedRectangle()
		{
			var xMin = Points.Where (point => !point.IsEmpty).Min (point => point.X) - LineWidth / 2;
			var xMax = Points.Where (point => !point.IsEmpty).Max (point => point.X) + LineWidth / 2;
			var yMin = Points.Where (point => !point.IsEmpty).Min (point => point.Y) - LineWidth / 2;
			var yMax = Points.Where (point => !point.IsEmpty).Max (point => point.Y) + LineWidth / 2;

			xMin = Math.Max (xMin, 0);
			xMax = Math.Min (xMax, Width);
			yMin = Math.Max (yMin, 0);
			yMax = Math.Min (yMax, Height);

			return new RectF (xMin, yMin, xMax, yMax);
		}

		float getScaleFromSize (System.Drawing.SizeF size, float width, float height)
		{
			float scaleX = size.Width / width;
			float scaleY = size.Height / height;
			
			return Math.Min (scaleX, scaleY);
		}
		
		System.Drawing.SizeF getSizeFromScale (float scale, float inWidth, float inHeight)
		{
			float width = inWidth * scale;
			float height = inHeight * scale;
			
			return new System.Drawing.SizeF (width, height);
		}

		//Allow the user to import an array of points to be used to draw a signature in the view, with new
		//lines indicated by a System.Drawing.PointF.Empty in the array.
		public void LoadPoints (System.Drawing.PointF[] loadedPoints)
		{
			if (loadedPoints == null || loadedPoints.Count () == 0)
				return;
			
			var startIndex = 0;
			var emptyIndex = loadedPoints.ToList ().IndexOf (System.Drawing.PointF.Empty);
			
			if (emptyIndex == -1)
				emptyIndex = loadedPoints.Count ();
			
			//Clear any existing paths or points.
			paths = new List<Path> ();
			points = new List<System.Drawing.PointF[]> ();
			
			do {
				//Create a new path and set the line options
				currentPath = new Path ();
				currentPoints = new List<System.Drawing.PointF> ();
				
				//Move to the first point and add that point to the current_points array.
				currentPath.MoveTo (loadedPoints [startIndex].X, loadedPoints [startIndex].Y);
				currentPoints.Add (loadedPoints [startIndex]);
				
				//Iterate through the array until an empty point (or the end of the array) is reached,
				//adding each point to the current_path and to the current_points array.
				for (var i = startIndex + 1; i < emptyIndex; i++) {
					currentPath.LineTo (loadedPoints [i].X, loadedPoints [i].Y);
					currentPoints.Add (loadedPoints [i]);
				}
				
				//Add the current_path and current_points list to their respective Lists before
				//starting on the next line to be drawn.
				paths.Add (currentPath);
				points.Add (currentPoints.ToArray ());
				
				//Obtain the indices for the next line to be drawn.
				startIndex = emptyIndex + 1;
				if (startIndex < loadedPoints.Count () - 1) {
					emptyIndex = loadedPoints.ToList ().IndexOf (System.Drawing.PointF.Empty, 
					                                             startIndex);
					
					if (emptyIndex == -1)
						emptyIndex = loadedPoints.Count ();
				} else
					emptyIndex = startIndex;
			} while (startIndex < emptyIndex);
			
			//Obtain the image for the imported signature and display it in the image view.
			imageView.SetImageBitmap (GetImage (false));

			//Display the clear button.
			lblClear.Visibility = ViewStates.Visible; 
			Invalidate ();
		}

		//Update the bounds for the rectangle to be redrawn if necessary for the given point.
		void updateBounds (float touchX, float touchY)
		{
			if (touchX < dirtyRect.Left)
				dirtyRect.Left = touchX;
			else if (touchX > dirtyRect.Right)
				dirtyRect.Right = touchX;
			
			if (touchY < dirtyRect.Top)
				dirtyRect.Top = touchY;
			else if (touchY > dirtyRect.Bottom)
				dirtyRect.Bottom = touchY;
		}

		//Set the bounds for the rectangle that will need to be redrawn to show the drawn path.
		void resetBounds (float touchX, float touchY)
		{
			if (touchX < lastX)
				dirtyRect.Left = touchX;
			if (touchX > lastX)
				dirtyRect.Right = touchX;
			if (touchY < lastY)
				dirtyRect.Top = touchY;
			if (touchY > lastY)
				dirtyRect.Bottom = touchY;
		}

		public override bool OnTouchEvent (MotionEvent e)
		{
			float touchX = e.GetX ();
			float touchY = e.GetY ();

			System.Drawing.PointF touch = new System.Drawing.PointF (touchX, touchY);

			switch (e.Action) {
			case MotionEventActions.Down:
				lastX = touchX;
				lastY = touchY;

				//Create a new path and move to the touched point.
				currentPath = new Path();
				currentPath.MoveTo (touchX, touchY);

				//Clear the list of points then add the touched point
				currentPoints.Clear ();
				currentPoints.Add (touch);

				//Display the clear button
				lblClear.Visibility = ViewStates.Visible;
				return true;
			case MotionEventActions.Move:
				handleTouch (e);
				Invalidate(
					(int) (dirtyRect.Left - 1),
					(int) (dirtyRect.Top - 1),
					(int) (dirtyRect.Right + 1),
					(int) (dirtyRect.Bottom + 1));
				break;
			case MotionEventActions.Up:
				handleTouch (e);
				currentPath = smoothedPathWithGranularity (20, out currentPoints);

				//Add the current path and points to their respective lists.
				paths.Add (currentPath);
				points.Add (currentPoints.ToArray ());

				//Get an image of the current signature and display it so that the entire set of paths
				//doesn't have to be redrawn every time.
				imageView.SetImageBitmap (GetImage (false));
				Invalidate ();
				break;
			default:
				return false;
			}
			
			lastX = touchX;
			lastY = touchY;
			
			return true;
		}

		//Iterate through the touch history since the last touch event and add them to the path and points list.
		void handleTouch (MotionEvent e)
		{
			float touchX = e.GetX ();
			float touchY = e.GetY ();

			System.Drawing.PointF touch = new System.Drawing.PointF (touchX, touchY);

			resetBounds (touchX, touchY);
			
			for (var i = 0; i < e.HistorySize; i++) {
				float historicalX = e.GetHistoricalX(i);
				float historicalY = e.GetHistoricalY(i);

				System.Drawing.PointF historical = new System.Drawing.PointF (historicalX, historicalY);

				updateBounds (historicalX, historicalY);

				currentPath.LineTo (historicalX, historicalY);
				currentPoints.Add (historical);
			}

			currentPath.LineTo (touchX, touchY);
			currentPoints.Add (touch);
		}

		protected override void OnDraw (Canvas canvas)
		{
			if (currentPath == null || currentPath.IsEmpty)
				return;

			canvas.DrawPath (currentPath, paint);
		}

		protected override void OnLayout (bool changed, int l, int t, int r, int b)
		{
			imageView.Layout (0, 0, Width, Height);
			signatureLine.Layout (10, Height - 35, Width - 10, Height - 34);
			lblClear.Layout (Width - 70, 10, Width - 10, 40);
			lblSign.Layout (Width / 2 - 45, Height - 35, Width / 2 + 45, Height);
			xLabel.Layout (11, Height - 65, 30, Height - 40);
		}

		Path smoothedPathWithGranularity (int granularity, out List<System.Drawing.PointF> smoothedPoints)
		{
			List<System.Drawing.PointF> pointsArray = currentPoints;
			smoothedPoints = new List<System.Drawing.PointF> ();

			//Not enough points to smooth effectively, so return the original path and points.
			if (pointsArray.Count < 4) {
				smoothedPoints = pointsArray;
				return currentPath;
			}

			//Create a new bezier path to hold the smoothed path.
			Path smoothedPath = new Path ();
	
			//Duplicate the first and last points as control points.
			pointsArray.Insert (0, pointsArray [0]);
			pointsArray.Add (pointsArray [pointsArray.Count - 1]);

			//Add the first point
			smoothedPath.MoveTo (pointsArray [0].X, pointsArray [0].Y);
			smoothedPoints.Add (pointsArray [0]);

			for (var index = 1; index < pointsArray.Count - 2; index++) {
				System.Drawing.PointF p0 = pointsArray [index - 1];
				System.Drawing.PointF p1 = pointsArray [index];
				System.Drawing.PointF p2 = pointsArray [index + 1];
				System.Drawing.PointF p3 = pointsArray [index + 2];

				//Add n points starting at p1 + dx/dy up until p2 using Catmull-Rom splines
				for (var i = 1; i < granularity; i++) {
					float t = (float)i * (1f / (float)granularity);
					float tt = t * t;
					float ttt = tt * t;

					//Intermediate point
					System.Drawing.PointF mid = new System.Drawing.PointF ();
					mid.X = 0.5f * (2f * p1.X + (p2.X - p0.X) * t + 
					                (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * tt + 
					                (3f * p1.X - p0.X - 3f * p2.X + p3.X) * ttt);
					mid.Y = 0.5f * (2 * p1.Y + (p2.Y - p0.Y) * t + 
					                (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * tt + 
					                (3 * p1.Y - p0.Y - 3 * p2.Y + p3.Y) * ttt);

					smoothedPath.LineTo (mid.X, mid.Y);
					smoothedPoints.Add (mid);
				}

				//Add p2
				smoothedPath.LineTo (p2.X, p2.Y);
				smoothedPoints.Add (p2);
			}

			//Add the last point
			System.Drawing.PointF last = pointsArray [pointsArray.Count - 1];
			smoothedPath.LineTo (last.X, last.Y);
			smoothedPoints.Add (last);

			return smoothedPath;
		}
	}
}
