namespace GetSemanticScholarAuthorCitationGraph
{
    public class GraphColor
    {
        public int r;
        public int g;
        public int b;
        public double a;
        public GraphColor()
        {
            r = 0;
            g = 0;
            b = 0;
            a = 1.0;
        }
        public GraphColor(int red, int green, int blue, double alpha)
        {
            r = red;
            g = green;
            b = blue;
            a = alpha;
        }
        public static GraphColor GetColor(ColorEnum color)
        {
            switch (color)
            {
                case ColorEnum.red:
                    return new GraphColor(255, 0, 0, 1.0);
                case ColorEnum.green:
                    return new GraphColor(0, 255, 0, 1.0);
                case ColorEnum.orange:
                    return new GraphColor(255, 155, 0, 1.0);
                default:
                    break;
            }
            return new GraphColor(0, 0, 0, 1.0);
        }
    }
    public enum ColorEnum
    {
        red = 0,
        green = 1,
        orange = 2
    }
}