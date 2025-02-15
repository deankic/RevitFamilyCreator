using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Linq;
using System.Net;

namespace FG_PROGRAM.PanelBuilder
{
    [Transaction(TransactionMode.Manual)]

    public class Panel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection selection = uidoc.Selection;
            var app = commandData.Application;

            using (Transaction tx = new Transaction(doc, "Place Family"))
            {
                tx.Start();
                try
                {
                    // Get face and ensure it has references
                    Reference faceRef = selection.PickObject(ObjectType.Face, "Select a face");
                    Element faceElement = doc.GetElement(faceRef);
                    Face selectedFace = faceElement.GetGeometryObjectFromReference(faceRef) as Face;


                    // Get edge
                    Reference edgeRef = selection.PickObject(ObjectType.Edge, "Select an edge");
                    Element edgeElem = doc.GetElement(edgeRef);
                    Edge selectedEdge = edgeElem.GetGeometryObjectFromReference(edgeRef) as Edge;
                    Line selectedLine = selectedEdge.AsCurve() as Line;

                    // Handle different element types
                    Reference finalFaceRef = null;
                    Line finalLine = null;
                    if (faceElement is Autodesk.Revit.DB.Panel)
                    {
                        // For curtain panels, we need to get the geometry directly
                        var options = new Options();
                        options.ComputeReferences = true;

                        // try using instance solid
                        GeomUtil.GetInstanceSolid(faceElement, options, out Solid instanceSolid, out Transform Instancetransform);
                        // try using symbol solid
                        GeomUtil.GetSymbolSolid(faceElement, options, out Solid symbolSolid, out Transform Symboltransform);

                        Face targetFaceInstance = null;

                        if (instanceSolid != null)
                        {
                            foreach (Face face in instanceSolid.Faces)
                            {
                                // Compare face properties to find match
                                if (GeometryEquals(face, selectedFace))
                                {
                                    targetFaceInstance = face;
                                    break;
                                }
                            }
                        }
                        Face targetFaceSymbol = null;

                        // If not found in instance, try symbol geometry
                        if (symbolSolid != null)
                        {
                            foreach (Face face in symbolSolid.Faces)
                            {
                                if (GeometryEquals(face, selectedFace))
                                {
                                    targetFaceSymbol = face;
                                    break;
                                }
                            }

                        }

                        // Find matching face in instance geometry
                        Face targetFace = null;

                        if (targetFaceInstance != null)
                        {
                            //targetFaceInstance.Visualize(doc);
                            targetFace = targetFaceInstance;
                        }
                        else if (targetFaceSymbol != null)
                        {
                            //targetFaceSymbol.Visualize(doc);
                            targetFace = targetFaceSymbol;
                        }

                        finalFaceRef = targetFace.Reference;


                        if (finalLine == null)
                        {
                            finalLine = selectedLine;
                        }

                    }

                    else
                    {
                        finalFaceRef = faceRef;
                        finalLine = selectedLine;
                    }

                    // Get family symbol
                    var symbol = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralStiffener)
                        .WhereElementIsElementType()
                        .First() as FamilySymbol;

                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                    }

                    // Create
                    // the family instance
                    var fam = doc.Create.NewFamilyInstance(finalFaceRef, finalLine, symbol);

                    tx.Commit();
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    tx.RollBack();
                    return Result.Failed;
                }
            }


        }

        private bool GeometryEquals(Face face1, Face face2)
        {
            // Compare face properties to determine if they're the same
            if (face1.Area.IsAlmostEqual(face2.Area))
            {
                XYZ normal1 = face1.ComputeNormal(new UV(0.5, 0.5));
                XYZ normal2 = face2.ComputeNormal(new UV(0.5, 0.5));
                return normal1.IsAlmostEqualTo(normal2);
            }
            return false;
        }




    }

}
