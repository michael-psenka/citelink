using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

// ****************************************************************************
// This program takes a PDF file (typically of a research paper), and converts
// all links to references in the bibliography to hyperlinks taking to the paper
// itself, saving a lot of back-and-forth time when reading background
// literature for papers.

// Calling without arguments will run the program on the most recently modified
// PDF in the current working directory. Optionally, you can run with a filename
// to specify what pdf should be modified (e.g. pdf_link_changer.exe paper.pdf).
// ****************************************************************************

class Program
{
    static void Main(string[] args)
    {
        // Get the most recently modified PDF file in the current directory
        string directory = Directory.GetCurrentDirectory();
        string[] files = Directory.GetFiles(directory, "*.pdf");
        string pdf_file = "";

        // if no pdf files found, exit
        if (files.Length == 0)
        {
            Console.WriteLine("No PDF files found in current directory. Exiting...");
            return;
        }
        // if filename specified, use that
        if (args.Length > 0)
        {
            string arg_input = args[0];
            // check if the file exists, or if .pdf was just missing
            if (Directory.GetFiles(directory, arg_input).Length == 0)
            {
                if (Directory.GetFiles(directory, arg_input + ".pdf").Length == 0)
                {
                    Console.WriteLine("File " + args[0] + " not found in current directory. Exiting...");
                    return;
                }
                else
                {
                    arg_input = arg_input + ".pdf";
                }
            }
            pdf_file = Directory.GetFiles(directory, arg_input)[0];
        }
        else
        {
            // make sure not to use any pdf that starts with "output"
            IOrderedEnumerable<string> orderedFiles =
                    files.OrderByDescending(f => new FileInfo(f).LastWriteTime);
            IEnumerator<string> enumerator = orderedFiles.GetEnumerator();

            while (enumerator.MoveNext())
            {
                // note that enumerator.Current contains full path. Need to extract filename
                if (!System.IO.Path.GetFileName(enumerator.Current).StartsWith("output"))
                {
                    pdf_file = enumerator.Current;
                    break;
                }
            }
        }
        Console.WriteLine("Processing file: " + pdf_file);
        PdfReader reader = new PdfReader(pdf_file);

        // get all named destinations, will need to retrieve from link
        Dictionary<object, iTextSharp.text.pdf.PdfObject> namedDestinations = reader.GetNamedDestination();

        // if output.pdf exists, we want to create output_1.pdf. If this exists, output_2.pdf, etc.
        string output_file = "output.pdf";
        int count_file = 1;
        while (File.Exists(output_file))
        {
            output_file = "output_" + count_file + ".pdf";
            count_file++;
        }

        PdfStamper stamper = new PdfStamper(reader, new FileStream(output_file, FileMode.Create));

        for (int i = 1; i <= reader.NumberOfPages; i++)
        {
            // progress update
            Console.Write("\rProcessing page {0} of {1}", i, reader.NumberOfPages);
            PdfDictionary page = reader.GetPageN(i);
            // get all links
            PdfArray annots = page.GetAsArray(PdfName.ANNOTS);
            if (annots == null) continue;
            for (int j = 0; j < annots.Size; j++)
            {
                PdfDictionary annotation = annots.GetAsDict(j);
                // get action dictionary
                PdfDictionary actionDictionary = annotation.GetAsDict(PdfName.A);
                // make sure there is an action dictionary before querying it
                if (actionDictionary == null) continue;
                // only care about links that navigate within PDF
                if (!actionDictionary.Get(PdfName.S).Equals(PdfName.GOTO)) continue;
                PdfObject destinationObject = actionDictionary.Get(PdfName.D);

                if (destinationObject == null) continue;
                if (destinationObject.IsString())
                {
                    string destinationString = destinationObject.ToString();
                    // check if this would normally link to a paper in bibliography
                    if (destinationString.StartsWith("cite"))
                    {
                        // get destination object from named destinations
                        PdfArray destObject = (PdfArray)(namedDestinations[destinationString]);
                        // get page from destObject
                        PdfObject pageObject = destObject[0];
                        // extract page dictionary from page object
                        PdfDictionary pageDict = (PdfDictionary)PdfReader.GetPdfObject(pageObject);

                        // extract page number, by looping through all pages (starting from the last page)
                        // until we find a page. I wish there was a better way to do this.
                        // TODO: find a non-inner-loop way. Not the end of the world since we usually
                        // expect bibliography to be at the end of the doc, but still.
                        int pageNumber = 0;
                        for (int k = reader.NumberOfPages; k > 0; k--)
                        {
                            PdfDictionary currentPageDict = reader.GetPageN(k);
                            if (currentPageDict.Equals(pageDict))
                            {
                                pageNumber = k;
                                break;
                            }
                        }

                        // Get the starting position
                        float top = ((PdfNumber)destObject[3]).FloatValue;
                        float left = ((PdfNumber)destObject[2]).FloatValue;

                        // get page width and height
                        // float pageWidth = pageDict.GetAsNumber(PdfName.WIDTH).FloatValue;
                        // float pageHeight = pageDict.GetAsNumber(PdfName.HEIGHT).FloatValue;
                        iTextSharp.text.Rectangle pageRect = reader.GetPageSize(1);

                        // Create a new instance of the custom extraction strategy
                        // note the format of iTextSharp rectangle (from ctrl+click) is
                        // ((bottom left xy),(top right xy)). However, behavior seems to be different.
                        // The intended area of this bounding box should have the citation start at
                        // the top-left corner of the box
                        var boundingBox = new iTextSharp.text.Rectangle(left, top, pageRect.Width, 0);

                        // Filter for page text within rectangle
                        RenderFilter[] filter = { new RegionTextRenderFilter(boundingBox) };
                        ITextExtractionStrategy strategy = new FilteredTextRenderListener(
                                new LocationTextExtractionStrategy(), filter);
                        string text = PdfTextExtractor.GetTextFromPage(reader, pageNumber, strategy);
                        // extracting based on the first appearance of a 4-digit number. Tried by new line
                        // but sometimes finnicky (there will be a little text and newline before the
                        // citation starts on rare occasion).
                        // Philosophy is that this will always terminate, either pub year or arxiv number.
                        // TODO: probably a better way to do this?

                        // Cut off text at the first occurance of 4 digits in a row
                        string textCutoff = Regex.Split(text, @"\d{4}")[0];
                        // in case for whatever reason a year is not present, cut off search at 400 characters
                        if (textCutoff.Length > 400)
                        {
                            textCutoff = textCutoff.Substring(0, 400);
                        }


                        // ****** FORMAT LINK: we google search the paper and click the first result
                        // remove \r and \n characters to put into URL search
                        // 
                        string textCleaned = textCutoff.Replace("\r", "").Replace("\n", "");
                        // remove Google's reserved characters
                        textCleaned = textCleaned.Replace("(", "").Replace(")", "").Replace("“", "").Replace("\"", "");
                        textCleaned = textCleaned.Replace("_", "").Replace("*", "").Replace("-", "").Replace("|", "").Replace("..", "");
                        // replace any space character with "+"
                        textCleaned = textCleaned.Replace(" ", "+");

                        string textNewUrl = "http://www.google.com/search?q=" + textCleaned + "&btnI";


                        // ****** SET NEW LINK: finally, we change annotation so that it is now a
                        // url link to the constructed google search & click

                        // modify action dictionry so it becomes of type URI
                        // with our constructed URI
                        actionDictionary.Put(PdfName.S, PdfName.URI);
                        actionDictionary.Put(PdfName.URI, new PdfString(textNewUrl));

                    }
                }
            }
        }
        // close pdf readers
        stamper.Close();
        reader.Close();
        // now, delete original pdf and replace with our new output.pdf
        File.Delete(pdf_file);
        File.Move(output_file, pdf_file);
        Console.WriteLine("\nDone!");
    }
}
