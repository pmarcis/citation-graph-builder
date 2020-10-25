# Semantic Scholar Citation Graph Builder

Semantic Scholar citation graph builder - allows to build a graph that includes all citations and (if specified) also references for all papers of an author given the Semantic Scholar author ID.

---
**NOTE 1**
Semantic Scholar: 1) may not have all information of an author's works, 2) tends to have noisy data - author data scattered around multiple author entries and multiple different authors may be merged under one author ID. So ... be transparent about these points when comparing data!

---
**NOTE 2**
Be polite! Semantic Scholar allows 100 requests per 5 minutes. If you do not want to get banned, do not increase the request frequency (and do not execute the requests in parallel)!

---

# Build Instructions

The code is written in C#. You will need Visual Studio or an alternative (C# capable) development environment to compile and use the code.

You may need Visual Studio 2019 or newer to compile the code.

# Usage Instructions

In order to use the `ss-citation-graph-builder`, first you need to acquire an author ID. Go to https://www.semanticscholar.org/, find the author you are interested in, open his/her profile, and copy the multi-digit number that comes after the author's name in the URL.

Then, execute the `GetSemanticScholarAuthorCitationGraph.exe` tool with the following command line:

```bash
.\GetSemanticScholarAuthorCitationGraph -o [OutFile] -id [AuthorID] -y [Year]
```

Replace:

* `[OutFile]` with a path to the Graph Exchange XML Format (GEXF) output file. This is where the graph will be stored.
* `[AuthorID]` with the multi-digit author ID.
* `[Year]` with the earliest year since which papers should be included in the graph. This allows you to analyse, for instance, papers for the last five years. The parameter `-y` is optional.

There are two optional parameters available (both override each other so only one should be specified):

* `--include-relevant-references` - include also references to co-author papers.
* `--include-all-references` - include all references.

The tool will output also statistics of each author after analysis. The format of the output is as follows:

```
[AuthorID]\t[AuthorName]\t[PaperCount]\t[SelfCitationCount]\t[CoAuthorCitationCount]\t[OtherCitationCount]
```

The GEXF graph can be visualised, for instance, using [Gephi](https://gephi.org/).

# Reference

If you use the code for scientific purposes, please refer to it using:

```bibtex
@ebook{ss-citation-graph-builder,
  author = {Pinnis, Mārcis},
  title  = {{Semantic Scholar Citation Graph Builder}},
  url    = {https://github.com/pmarcis/ss-citation-graph-builder},
  year   = {2020},
  note   = {Accessed Oct. 25, 2020}
}
```
