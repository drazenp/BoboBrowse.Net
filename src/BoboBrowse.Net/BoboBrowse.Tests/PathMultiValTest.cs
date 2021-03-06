﻿//* Bobo Browse Engine - High performance faceted/parametric search implementation 
//* that handles various types of semi-structured data.  Originally written in Java.
//*
//* Ported and adapted for C# by Shad Storhaug.
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
namespace BoboBrowse.Net
{
    using BoboBrowse.Net.Facets;
    using BoboBrowse.Net.Facets.Impl;
    using BoboBrowse.Net.Support;
    using Lucene.Net.Analysis;
    using Lucene.Net.Documents;
    using Lucene.Net.Index;
    using Lucene.Net.Store;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [TestFixture]
    public class PathMultiValTest // Original name was TestPathMultiVal.java
    {
        private RAMDirectory directory;
	    private Analyzer analyzer;
	    private List<IFacetHandler> facetHandlers;

        const string PathHandlerName = "path";

        [SetUp]
        public void Init()
        {
            facetHandlers = new List<IFacetHandler>();

            directory = new RAMDirectory();
            analyzer = new WhitespaceAnalyzer();
            IndexWriter writer = new IndexWriter(directory, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED);
            Document doc = new Document();
            AddMetaDataField(doc, PathHandlerName, new String[] { "/a/b/c", "/a/b/d" });
            writer.AddDocument(doc);
            writer.Commit();

            PathFacetHandler pathHandler = new PathFacetHandler("path", true);
            facetHandlers.Add(pathHandler);
        }

        [TearDown]
        public void Dispose()
        {
            facetHandlers = null;
            if (directory.isOpen_ForNUnit) directory.Dispose();
            directory = null;
            analyzer = null;
        }

        private void AddMetaDataField(Document doc, String name, String[] vals)
        {
            foreach (String val in vals)
            {
                Field field = new Field(name, val, Field.Store.NO, Field.Index.NOT_ANALYZED_NO_NORMS);
                field.OmitTermFreqAndPositions = (true);
                doc.Add(field);
            }
        }

        [Test]
        public void TestMultiValPath()
        {
            IndexReader reader = IndexReader.Open(directory, true);
            BoboIndexReader boboReader = BoboIndexReader.GetInstance(reader, facetHandlers);

            BoboBrowser browser = new BoboBrowser(boboReader);
            BrowseRequest req = new BrowseRequest();

            BrowseSelection sel = new BrowseSelection(PathHandlerName);
            sel.AddValue("/a");
            var propMap = new Dictionary<String, String>();
            propMap.Put(PathFacetHandler.SEL_PROP_NAME_DEPTH, "0");
            propMap.Put(PathFacetHandler.SEL_PROP_NAME_STRICT, "false");
            sel.SetSelectionProperties(propMap);

            req.AddSelection(sel);

            FacetSpec fs = new FacetSpec();
            fs.MinHitCount = (1);
            req.SetFacetSpec(PathHandlerName, fs);

            BrowseResult res = browser.Browse(req);
            Assert.AreEqual(res.NumHits, 1);
            IFacetAccessible fa = res.GetFacetAccessor(PathHandlerName);
            IEnumerable<BrowseFacet> facets = fa.GetFacets();
            Console.WriteLine(facets);
            Assert.AreEqual(1, facets.Count());
            BrowseFacet facet = facets.Get(0);
            Assert.AreEqual(2, facet.FacetValueHitCount);
        }
    }
}
