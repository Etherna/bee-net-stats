// Copyright 2024-present Etherna SA
// This file is part of Bee.Net Stats.
// 
// Bee.Net Stats is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Bee.Net Stats is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Bee.Net Stats.
// If not, see <https://www.gnu.org/licenses/>.

namespace Etherna.BeeNetStats
{
    public class OutputCsvRecord(
        string sourceFileSize,
        ushort compactLevel,
        double avgDepth,
        double avgSeconds)
    {
        public string SourceFileSize { get; } = sourceFileSize;
        public ushort CompactLevel { get; } = compactLevel;
        public double AvgDepth { get; } = avgDepth;
        public double AvgSeconds { get; } = avgSeconds;
    }
}