namespace GitletSharp
{
    internal class Setting
    {
        private readonly string _name;
        private readonly string _value;

        public Setting(string name, string value)
        {
            _name = name;
            _value = value;
        }

        public string Name { get { return _name; } }
        public string Value { get { return _value; } }
    }
}
