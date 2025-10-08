namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Represents a position on the canvas with X and Y coordinates
    /// </summary>
    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Position()
        {
        }

        public Position(double x, double y)
        {
            X = x;
            Y = y;
        }

        public Position Clone()
        {
            return new Position(X, Y);
        }

        public override string ToString()
        {
            return $"X:{X}, Y:{Y}";
        }
    }
}