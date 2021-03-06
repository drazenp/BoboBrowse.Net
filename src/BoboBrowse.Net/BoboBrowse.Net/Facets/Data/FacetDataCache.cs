﻿//* Bobo Browse Engine - High performance faceted/parametric search implementation 
//* that handles various types of semi-structured data.  Originally written in Java.
//*
//* Ported and adapted for C# by Shad Storhaug, Alexey Shcherbachev, and zhengchun.
//*
//* Copyright (C) 2005-2015  John Wang
//*
//* Licensed under the Apache License, Version 2.0 (the "License");
//* you may not use this file except in compliance with the License.
//* You may obtain a copy of the License at
//*
//*   http://www.apache.org/licenses/LICENSE-2.0
//*
//* Unless required by applicable law or agreed to in writing, software
//* distributed under the License is distributed on an "AS IS" BASIS,
//* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//* See the License for the specific language governing permissions and
//* limitations under the License.

// Version compatibility level: 3.2.0
namespace BoboBrowse.Net.Facets.Data
{
    using BoboBrowse.Net.Sort;
    using BoboBrowse.Net.Util;
    using Common.Logging;
    using Lucene.Net.Index;
    using Lucene.Net.Search;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [Serializable]
    public class FacetDataCache
    {
        private static ILog logger = LogManager.GetLogger(typeof(FacetDataCache));

        //private readonly static long serialVersionUID = 1L; // NOT USED

        protected BigSegmentedArray orderArray;
        protected ITermValueList valArray;
        protected int[] freqs;
        protected int[] minIDs;
        protected int[] maxIDs;

        public FacetDataCache(BigSegmentedArray orderArray, ITermValueList valArray, int[] freqs, int[] minIDs, 
            int[] maxIDs, TermCountSize termCountSize)
        {
            this.orderArray = orderArray;
            this.valArray = valArray;
            this.freqs = freqs;
            this.minIDs = minIDs;
            this.maxIDs = maxIDs;
        }

        public FacetDataCache()
        {
            this.orderArray = null;
            this.valArray = null;
            this.maxIDs = null;
            this.minIDs = null;
            this.freqs = null;
        }

        public virtual ITermValueList ValArray
        {
            get { return this.valArray; }
            internal set { this.valArray = value; }
        }

        public virtual BigSegmentedArray OrderArray
        {
            get { return this.orderArray; }
            internal set { this.orderArray = value; }
        }

        public virtual int[] Freqs
        {
            get { return freqs; }
            internal set { freqs = value; }
        }

        public virtual int[] MinIDs
        {
            get { return minIDs; }
            internal set { minIDs = value; }
        }

        public virtual int[] MaxIDs
        {
            get { return maxIDs; }
            internal set { maxIDs = value; }
        }

        

        public virtual int GetNumItems(int docid)
        {
            int valIdx = orderArray.Get(docid);
            return valIdx <= 0 ? 0 : 1;
        }

        private static BigSegmentedArray NewInstance(int termCount, int maxDoc)
        {
            // we use < instead of <= to take into consideration "missing" value (zero element in the dictionary)
            if (termCount < sbyte.MaxValue)
            {
                return new BigByteArray(maxDoc);
            }
            else if (termCount < short.MaxValue)
            {
                return new BigShortArray(maxDoc);
            }
            else
                return new BigIntArray(maxDoc);
        }

        protected int GetDictValueCount(IndexReader reader, string field)
        {
            int ret = 0;
            using (TermEnum termEnum = reader.Terms(new Term(field, "")))
            {
                do
                {
                    Term term = termEnum.Term;
                    if (term == null || string.CompareOrdinal(term.Field, field) != 0)
                        break;
                    ret++;
                } while (termEnum.Next());
            }
            return ret;
        }

        protected int GetNegativeValueCount(IndexReader reader, string field)
        {
            int ret = 0;
            using (TermEnum termEnum = reader.Terms(new Term(field, "")))
            {
                do
                {
                    Term term = termEnum.Term;
                    if (term == null || string.CompareOrdinal(term.Field, field) != 0)
                        break;
                    if (!term.Text.StartsWith("-"))
                    {
                        break;
                    }
                    ret++;
                } while (termEnum.Next());
            }
            return ret;
        }

        public virtual void Load(string fieldName, IndexReader reader, TermListFactory listFactory)
        {
            string field = string.Intern(fieldName);
            int maxDoc = reader.MaxDoc;

            BigSegmentedArray order = this.orderArray;
            if (order == null) // we want to reuse the memory
            {
                int dictValueCount = GetDictValueCount(reader, fieldName);
                order = NewInstance(dictValueCount, maxDoc);
            }
            else
            {
                order.EnsureCapacity(maxDoc); // no need to fill to 0, we are reseting the 
                                              // data anyway
            }
            this.orderArray = order;

            List<int> minIDList = new List<int>();
            List<int> maxIDList = new List<int>();
            List<int> freqList = new List<int>();

            int length = maxDoc + 1;
            ITermValueList list = listFactory == null ? (ITermValueList)new TermStringList() : listFactory.CreateTermList();
            int negativeValueCount = GetNegativeValueCount(reader, field);

            TermDocs termDocs = reader.TermDocs();
            TermEnum termEnum = reader.Terms(new Term(field, ""));
            int t = 0; // current term number

            list.Add(null);
            minIDList.Add(-1);
            maxIDList.Add(-1);
            freqList.Add(0);
            int totalFreq = 0;
            //int df = 0;
            t++;
            try
            {
                do
                {
                    Term term = termEnum.Term;
                    if (term == null || string.CompareOrdinal(term.Field, field) != 0)
                        break;

                    // store term text
                    // we expect that there is at most one term per document

                    // Alexey: well, we could get now more than one term per document. Effectively, we could build facet against tokenized field
                    //if (t >= length)
                    //{
                    //    throw new RuntimeException("there are more terms than " + "documents in field \"" + field 
                    //        + "\", but it's impossible to sort on " + "tokenized fields");
                    //}
                    list.Add(term.Text);
                    termDocs.Seek(termEnum);
                    // freqList.add(termEnum.docFreq()); // doesn't take into account deldocs
                    int minID = -1;
                    int maxID = -1;
                    int df = 0;
                    int valId = (t - 1 < negativeValueCount) ? (negativeValueCount - t + 1) : t;
                    if (termDocs.Next())
                    {
                        df++;
                        int docid = termDocs.Doc;
                        order.Add(docid, valId);
                        minID = docid;
                        while (termDocs.Next())
                        {
                            df++;
                            docid = termDocs.Doc;
                            order.Add(docid, valId);
                        }
                        maxID = docid;
                    }
                    freqList.Add(df);
                    totalFreq += df;
                    minIDList.Add(minID);
                    maxIDList.Add(maxID);

                    t++;
                } while (termEnum.Next());
            }
            finally
            {
                termDocs.Dispose();
                termEnum.Dispose();
            }
            list.Seal();
            this.valArray = list;
            this.freqs = freqList.ToArray();
            this.minIDs = minIDList.ToArray();
            this.maxIDs = maxIDList.ToArray();

            int doc = 0;
            while (doc <= maxDoc && order.Get(doc) != 0)
            {
                ++doc;
            }
            if (doc <= maxDoc)
            {
                this.minIDs[0] = doc;
                // Try to get the max
                doc = maxDoc;
                while (doc > 0 && order.Get(doc) != 0)
                {
                    --doc;
                }
                if (doc > 0)
                {
                    this.maxIDs[0] = doc;
                }
            }
            this.freqs[0] = maxDoc + 1 - totalFreq;
        }

        private static int[] ConvertString(FacetDataCache dataCache, string[] vals)
        {
            var list = new List<int>(vals.Length);
            for (int i = 0; i < vals.Length; ++i)
            {
                int index = dataCache.ValArray.IndexOf(vals[i]);
                if (index >= 0)
                {
                    list.Add(index);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// Same as ConvertString(FacetDataCache dataCache,string[] vals) except that the
        /// values are supplied in raw form so that we can take advantage of the type
        /// information to find index faster.
        /// </summary>
        /// <param name="dataCache"></param>
        /// <param name="vals"></param>
        /// <returns>the array of order indices of the values.</returns>
        public static int[] Convert<T>(FacetDataCache dataCache, T[] vals)
        {
            if (vals != null && (typeof(T) == typeof(string)))
            {
                var valsString = vals.Cast<string>().ToArray();
                return ConvertString(dataCache, valsString);
            }
            var list = new List<int>(vals.Length);
            for (int i = 0; i < vals.Length; ++i)
            {
                int index = -1;
                var valArrayTyped = dataCache.ValArray as TermValueList<T>;
                if (valArrayTyped != null)
                {
                    index = valArrayTyped.IndexOfWithType(vals[i]);
                }
                else
                {
                    index = dataCache.ValArray.IndexOf(vals[i]);
                }
                if (index >= 0)
                {
                    list.Add(index);
                }

            }
            return list.ToArray();
        }
    }

    public class FacetDocComparatorSource : DocComparatorSource
    {
        private IFacetHandler _facetHandler;

        public FacetDocComparatorSource(IFacetHandler facetHandler)
        {
            _facetHandler = facetHandler;
        }

        public override DocComparator GetComparator(IndexReader reader, int docbase)
        {
            if (!(reader.GetType().Equals(typeof(BoboIndexReader))))
                throw new ArgumentException("reader not instance of BoboIndexReader");
            BoboIndexReader boboReader = (BoboIndexReader)reader;
            FacetDataCache dataCache = _facetHandler.GetFacetData<FacetDataCache>(boboReader);
            BigSegmentedArray orderArray = dataCache.OrderArray;
            return new FacetDocComparator(dataCache, orderArray);
        }

        public class FacetDocComparator : DocComparator
        {
            private readonly FacetDataCache _dataCache;
            private readonly BigSegmentedArray _orderArray;

            public FacetDocComparator(FacetDataCache dataCache, BigSegmentedArray orderArray)
            {
                _dataCache = dataCache;
                _orderArray = orderArray;
            }

            public override IComparable Value(ScoreDoc doc)
            {
                int index = _orderArray.Get(doc.Doc);
                return _dataCache.ValArray.GetComparableValue(index);
            }

            public override int Compare(ScoreDoc doc1, ScoreDoc doc2)
            {
                return _orderArray.Get(doc1.Doc) - _orderArray.Get(doc2.Doc);
            }
        }
    }
}
