using System.Collections.Generic;

namespace GitletSharp
{
    internal class Section
    {
        private readonly string _name;
        private readonly string _label;
        private readonly IEnumerable<Setting> _settings;

        public Section(string name, string label, IEnumerable<Setting> settings)
        {
            _name = name;
            _label = label;
            _settings = settings;
        }

        public string Name { get { return _name; } }
        public string Label { get { return _label; } }
        public IEnumerable<Setting> Settings { get { return _settings; } }
    }
}
