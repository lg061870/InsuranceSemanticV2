namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Represents the size of an element on the canvas with Width and Height dimensions
    /// </summary>
    public class Size
    {
        public double Width { get; set; }
        public double Height { get; set; }

        public Size()
        {
        }

        public Size(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public Size Clone()
        {
            return new Size(Width, Height);
        }
        
        public override string ToString()
        {
            return $"Width:{Width}, Height:{Height}";
        }
    }
}