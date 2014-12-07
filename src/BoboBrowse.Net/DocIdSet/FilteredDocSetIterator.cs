﻿//* 
//* Copyright (C) 2005-2006  John Wang
//*
//* This library is free software; you can redistribute it and/or
//* modify it under the terms of the GNU Lesser General Public
//* License as published by the Free Software Foundation; either
//* version 2.1 of the License, or (at your option) any later version.
//*
//* This library is distributed in the hope that it will be useful,
//* but WITHOUT ANY WARRANTY; without even the implied warranty of
//* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//* Lesser General Public License for more details.
//*
//* You should have received a copy of the GNU Lesser General Public
//* License along with this library; if not, write to the Free Software
//* Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//* 
//* To contact the project administrators for the bobo-browse project, 
//* please go to https://sourceforge.net/projects/bobo-browse/, or 
//* send mail to owner@browseengine.com. 

// Version compatibility level: 3.1.0
namespace BoboBrowse.Net.DocIdSet
{
    using System;
    using Lucene.Net.Search;

    public abstract class FilteredDocSetIterator : DocIdSetIterator
    {
        protected DocIdSetIterator _innerIter;
        private int _currentDoc;

        protected FilteredDocSetIterator(DocIdSetIterator innerIter)
        {
            if (innerIter == null)
            {
                throw new ArgumentNullException("null iterator");
            }
            this._innerIter = innerIter;
            _currentDoc = -1;
        }

        protected abstract bool Match(int doc);

        public sealed override int DocID()
        {
            return _currentDoc;
        }

        public sealed override int NextDoc()
        {
            int docid = _innerIter.NextDoc();
            while (docid != DocIdSetIterator.NO_MORE_DOCS)
            {
                if (Match(docid))
                {
                    _currentDoc = docid;
                    return docid;
                }
                else
                {
                    docid = _innerIter.NextDoc();
                }
            }
            return DocIdSetIterator.NO_MORE_DOCS;
        }

        public sealed override int Advance(int target)
        {
            int docid = _innerIter.Advance(target);
            while (docid != DocIdSetIterator.NO_MORE_DOCS)
            {
                if (Match(docid))
                {
                    _currentDoc = docid;
                    return docid;
                }
                else
                {
                    docid = _innerIter.NextDoc();
                }
            }
            return DocIdSetIterator.NO_MORE_DOCS;
        }
    }
}
