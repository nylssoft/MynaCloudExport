﻿/*
    Myna Cloud Export
    Copyright (C) 2022 Niels Stockfleth

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
namespace CloudExport.Services
{
    public class DocumentItem
    {
        public long id { get; set; }

        public long? parentId { get; set; }

        public string name { get; set; } = string.Empty;

        public long size { get; set; }

        public string type { get; set; } = string.Empty;

        public int children { get; set; }

        public string accessRole { get; set; } = string.Empty;
    }
}
