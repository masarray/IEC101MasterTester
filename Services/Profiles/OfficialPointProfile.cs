using IEC101MasterTester.Models;
using System;
using System.Collections.Generic;

namespace IEC101MasterTester.Services.Profiles
{
    public sealed class OfficialPointProfile
    {
        private readonly Dictionary<int, PointDefinition> _pointsByIoa;
        private readonly Dictionary<string, PointDefinition> _pointsByKey;

        public OfficialPointProfile(string profileName, IEnumerable<PointDefinition> points)
        {
            ProfileName = profileName ?? "UnknownProfile";
            _pointsByIoa = new Dictionary<int, PointDefinition>();
            _pointsByKey = new Dictionary<string, PointDefinition>(StringComparer.OrdinalIgnoreCase);

            if (points == null)
            {
                return;
            }

            foreach (PointDefinition point in points)
            {
                if (point == null)
                {
                    continue;
                }

                _pointsByIoa[point.Ioa] = point;

                if (!string.IsNullOrWhiteSpace(point.PointKey))
                {
                    _pointsByKey[point.PointKey] = point;
                }
            }
        }

        public string ProfileName { get; }

        public bool TryGetByIoa(int ioa, out PointDefinition point)
        {
            return _pointsByIoa.TryGetValue(ioa, out point);
        }

        public bool TryGetByPointKey(string pointKey, out PointDefinition point)
        {
            point = null;
            if (string.IsNullOrWhiteSpace(pointKey))
            {
                return false;
            }

            return _pointsByKey.TryGetValue(pointKey, out point);
        }
    }
}
