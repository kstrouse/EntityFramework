﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Relational.Utilities;

namespace Microsoft.Data.Relational.Model
{
    public class Sequence
    {
        private readonly SchemaQualifiedName _name;
        private int _incrementBy = 1;
        private string _dataType = "BIGINT";

        public Sequence(SchemaQualifiedName name)
        {
            _name = name;
        }

        public virtual SchemaQualifiedName Name
        {
            get { return _name; }
        }

        public virtual int StartWith { get; set; }

        public virtual int IncrementBy
        {
            get { return _incrementBy; }
            set { _incrementBy = value; }
        }

        public virtual string DataType
        {
            get { return _dataType; }

            [param: NotNull]
            set
            {
                Check.NotEmpty(value, "value");

                _dataType = value;
            }
        }
    }
}