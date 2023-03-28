using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace cathode_vartype
{
    internal class Program
    {
        public class CathodeNode
        {
            public string nodeName = "";
            public string className = "";

            public List<CathodeParameter> nodeParameters = new List<CathodeParameter>();
            public List<CathodeParameter> nodeParametersFromGet = new List<CathodeParameter>();
        }
        public class CathodeParameter
        {
            public string parameterName = "";
            public string parameterVariableName = "";
            public string parameterVariableNameStripped = "";
            public string parameterVariableType = "";
            public string parameterDataType = "";
            public string parameterDataType_FROMFIRSTDUMP = "";
            public string parameterDefaultValue = "";
            public bool didFindFunction = false;
            public bool doesHaveDefaultValue = false;
        }

        public class DefaultVals
        {
            public DefaultVals(string a, string b)
            {
                VarToMatch = a;
                Value = b;
            }
            public string VarToMatch = "";
            public string Value = "";
        }

        public class FunctionData
        {
            public string functionName = "";
            public string functionNameStripped = "";
            public List<string> functionContent = new List<string>();

            public override string ToString()
            {
                return functionName;
            }
        }
        public enum FunctionReaderState
        {
            INSIDE_FUNCTION,
            OUTSIDE_FUNCTION,
        }

        public class DAT_Lookup
        {
            public string DAT_Name = "";
            public string DAT_Value = "";
        }

        /* Download assets required to generate docs if we don't already have them */
        static List<string> DownloadIfNotAlreadyPresent(string filename)
        {
            string path = "assets/";

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            if (!File.Exists(path + filename))
            {
                Console.WriteLine("Couldn't find \"" + filename + "\" - acquiring...");
                var request = WebRequest.Create("https://myfiles.mattfiler.co.uk/docs_assets/" + filename);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), ASCIIEncoding.ASCII))
                {
                    File.WriteAllText(path + filename, reader.ReadToEnd());
                }
            }

            return new List<string>(File.ReadAllLines(path + filename));
        }

        static void Main(string[] args)
        {
            #region Define default variable lookups 
            List<DefaultVals> defaultVariables = new List<DefaultVals>();
            //defaultVariables.Add(new DefaultVals("default_Enum", "0xffffffff00000000"));
            defaultVariables.Add(new DefaultVals("default_String", ""));
            #endregion

            #region Pull all entity/parameter names from the ShortGuid register dump
            List<string> mDump = DownloadIfNotAlreadyPresent("guid dump.txt");
            List<CathodeNode> entities = new List<CathodeNode>();
            List<string> paramtypedump = new List<string>();
            string iCantBeBotheredToCount = "CATHODE::ShortGuid::ShortGuid((ShortGuid *)&CATHODE::";
            for (int i = 0; i < mDump.Count; i++)
            {
                string thisNodeName = mDump[i].Substring(iCantBeBotheredToCount.Length).Split(new[] { "__" }, StringSplitOptions.None)[0];
                CathodeNode thisNode = entities.FirstOrDefault(o => o.className == thisNodeName);
                if (thisNode == null)
                {
                    thisNode = new CathodeNode();
                    string[] stringSplit = mDump[i].Split('"');
                    thisNode.nodeName = stringSplit[stringSplit.Length - 2];
                    thisNode.className = thisNodeName;
                    entities.Add(thisNode);
                }
                else
                {
                    CathodeParameter thisParam = new CathodeParameter();
                    string[] stringSplit = mDump[i].Split('"');
                    thisParam.parameterName = stringSplit[stringSplit.Length - 2];
                    string metadataname = mDump[i].Split(new[] { "__Class::" }, StringSplitOptions.None)[1].Split(',')[0];
                    string[] type_split = metadataname.Split('_');
                    thisParam.parameterVariableName = metadataname;
                    thisParam.parameterVariableType = type_split[type_split.Length - 2];
                    thisParam.parameterVariableNameStripped = metadataname.Substring(0, metadataname.Length - 5 - thisParam.parameterVariableType.Length - 1);
                    thisNode.nodeParameters.Add(thisParam);
                    if (!paramtypedump.Contains(thisParam.parameterVariableType)) paramtypedump.Add(thisParam.parameterVariableType);
                }
            }
            #endregion

            #region Pull content of functions for each entity from the iOS dump
            List<string> cDump = DownloadIfNotAlreadyPresent("AI ios.c");
            List<string> functionDump = new List<string>();
            List<FunctionData> functions = new List<FunctionData>();
            FunctionData currentFuncData = new FunctionData();
            string cathodeNamespace = "CATHODE::";
            FunctionReaderState currentFuncState = FunctionReaderState.OUTSIDE_FUNCTION;
            for (int i = 0; i < cDump.Count; i++)
            {
                switch (currentFuncState)
                {
                    case FunctionReaderState.OUTSIDE_FUNCTION:
                        if (cDump[i] == "{")
                        {
                            //We just entered a function
                            currentFuncState = FunctionReaderState.INSIDE_FUNCTION;
                            currentFuncData.functionName.Replace("\n", "").Replace("  ", "");
                            break;
                        }
                        if (cDump[i].Length >= 2 && cDump[i].Substring(0, 2) == "//")
                        {
                            break;
                        }
                        currentFuncData.functionName += cDump[i].Trim() + " ";
                        break;
                    case FunctionReaderState.INSIDE_FUNCTION:
                        if (cDump[i] == "}")
                        {
                            //We just left a function
                            currentFuncState = FunctionReaderState.OUTSIDE_FUNCTION;
                            string funcNameStart = currentFuncData.functionName.Split('(')[0].Trim();
                            string[] funcNameStartSplit = funcNameStart.Split(' ');
                            currentFuncData.functionName = funcNameStartSplit[funcNameStartSplit.Length - 1];
                            if (currentFuncData.functionName.Length >= cathodeNamespace.Length && currentFuncData.functionName.Substring(0, cathodeNamespace.Length) == cathodeNamespace)
                            {
                                string[] splitFuncName = currentFuncData.functionName.Split(new[] { "::" }, StringSplitOptions.None);
                                currentFuncData.functionNameStripped = splitFuncName[splitFuncName.Length - 1];
                                functionDump.Add(currentFuncData.functionName);
                                functions.Add(currentFuncData);
                            }
                            currentFuncData = new FunctionData();
                            break;
                        }
                        currentFuncData.functionContent.Add(cDump[i]);
                        break;
                }
            }
            functionDump.Sort();
            File.WriteAllLines("DEBUG_function_dump.txt", functionDump);
            #endregion

            #region Parse "on_custom_method" to dump custom methods and interfaces
            Dictionary<string, string> interfaceMappings = new Dictionary<string, string>();
            Dictionary<string, List<string>> customMethodMappings = new Dictionary<string, List<string>>();
            foreach (FunctionData functionData in functions)
            {
                if (functionData.functionNameStripped == "on_custom_method")
                {
                    string entity = functionData.functionName.Split(new[] { "::" }, StringSplitOptions.None)[1];

                    List<string> customMethods = new List<string>();
                    foreach (string line in functionData.functionContent)
                    {
                        if (line.Contains("CathodeProfileNode::CathodeProfileNode"))
                        {
                            customMethods.Add(line.Split(new[] { "CathodeProfileNode::CathodeProfileNode" }, StringSplitOptions.None)[1].Split('\"')[1]);
                        }
                        if (line.Contains("::on_custom_method"))
                        {
                            interfaceMappings.Add(entity, line.Split(new[] { "=" }, StringSplitOptions.None)[1].Substring(1).Split(new[] { "::" }, StringSplitOptions.None)[0]);
                        }
                    }
                    customMethodMappings.Add(entity, customMethods);
                }
            }
            #endregion

            #region Not all entities use on_custom_method, so also parse typeinfo to capture other interfaces
            List<string> cDump2 = DownloadIfNotAlreadyPresent("ios dump.txt");
            string start = "                             * typeinfo for CATHODE::";
            string start2 = "                             CATHODE::";
            string end = "                             **************************************************************";
            bool is_reading = false;
            string current_entity = "";
            int lineCount = 0;
            foreach (string line in cDump2)
            {
                if (!is_reading && line.Length >= start.Length && line.Substring(0, start.Length) == start)
                {
                    is_reading = true;
                    lineCount = 0;
                }
                if (is_reading && line.Length >= start2.Length && line.Substring(0, start2.Length) == start2)
                {
                    current_entity = line.Substring(start2.Length).Split(new[] { "::" }, StringSplitOptions.None)[0];
                    if (current_entity.Contains("MemoryAllocation<"))
                    {
                        is_reading = false;
                    }
                }
                if (is_reading && lineCount > 2 && line.Length >= end.Length && line.Substring(0, end.Length) == end)
                {
                    is_reading = false;
                }
                if (is_reading)
                {
                    lineCount++;
                    if (line.Contains("addr       CATHODE::"))
                    {
                        string interfaceName = line.Split(new[] { "addr       CATHODE::" }, StringSplitOptions.None)[1].Split(new[] { "::typeinfo" }, StringSplitOptions.None)[0];
                        if (!interfaceMappings.ContainsKey(current_entity))
                            interfaceMappings.Add(current_entity, interfaceName);
                        //Console.WriteLine(current_entity + " -> " + interfaceName);
                    }
                }
            }
            #endregion

            #region Parse alternate names
            List<string> tDump = DownloadIfNotAlreadyPresent("parameter_types.txt");
            List<DAT_Lookup> datLookup = new List<DAT_Lookup>();
            string shortGUIDDef = "CATHODE::ShortGuid::ShortGuid((ShortGuid*)&";
            for (int i = 0; i < tDump.Count; i++)
            {
                if (tDump[i].Length >= shortGUIDDef.Length && tDump[i].Substring(0, shortGUIDDef.Length) == shortGUIDDef)
                {
                    string a = tDump[i].Substring(shortGUIDDef.Length).Trim();
                    a = a.Substring(0, a.Length - 3);
                    if (a.Substring(0, 4) == "DAT_" || a.Substring(0, 4) == "__Me")
                    {
                        string[] b = a.Split(new char[] { ',' }, 2);

                        DAT_Lookup thisDat = new DAT_Lookup();
                        thisDat.DAT_Name = b[0];
                        thisDat.DAT_Value = b[1].Substring(1);
                        datLookup.Add(thisDat);

                        tDump[i] = "";
                    }
                    else
                    {
                        if (a.Substring(0, "CATHODE::".Length) == "CATHODE::")
                        {
                            tDump[i] = tDump[i].Substring(shortGUIDDef.Length);
                            tDump[i] = tDump[i].Trim();
                            tDump[i] = tDump[i].Substring(0, tDump[i].Length - 3);
                            string[] b = tDump[i].Split(new[] { "_type," }, StringSplitOptions.None);
                            tDump[i] = b[0] + "_type = " + b[1].Substring(1) + ";";
                        }
                    }
                }
            }
            //File.WriteAllLines("tdump_1.txt", tDump);
            List<string> dump = new List<string>();
            for (int i = 0; i < tDump.Count; i++)
            {
                if (tDump[i] == "") continue;
                if (!(tDump[i].Length >= shortGUIDDef.Length && tDump[i].Substring(0, shortGUIDDef.Length) == shortGUIDDef))
                {
                    string a = tDump[i].Substring("CATHODE::".Length);
                    string[] b = a.Split(new[] { "__Class::" }, StringSplitOptions.None);
                    string[] c = b[1].Split(new[] { "=" }, StringSplitOptions.None);

                    string ClassName = b[0];
                    string VariableName = c[0].Trim();
                    VariableName = VariableName.Substring(0, VariableName.Length - "_type".Length);
                    string[] e = VariableName.Split('_');
                    VariableName = VariableName.Substring(0, VariableName.Length - e[e.Length - 1].Length - 1);
                    VariableName = VariableName.Replace(shortGUIDDef, "");
                    string VariableType = c[1].Trim();
                    VariableType = VariableType.Substring(0, VariableType.Length - 1);

                    for (int x = 0; x < datLookup.Count; x++)
                    {
                        if (datLookup[x].DAT_Name == VariableType.Replace("CATHODE::", ""))
                        {
                            VariableType = datLookup[x].DAT_Value;
                        }
                    }

                    for (int x = 0; x < entities.Count; x++)
                    {
                        if (entities[x].className == ClassName)
                        {
                            for (int z = 0; z < entities[x].nodeParameters.Count; z++)
                            {
                                if (entities[x].nodeParameters[z].parameterVariableNameStripped == VariableName)
                                {
                                    entities[x].nodeParameters[z].parameterDataType_FROMFIRSTDUMP = VariableType;
                                }
                            }
                        }
                    }

                    //Console.WriteLine(ClassName + "::" + VariableName + " = " + VariableType);
                    dump.Add(ClassName + "::" + VariableName + " = " + VariableType);
                }
            }
            //File.WriteAllLines("out_dump.txt", dump);
            #endregion

            goto jump_to_dump;

            #region Work out parameter defaults from C dump per entity/interface
            List<string> debug_out = new List<string>();
            for (int i = 0; i < entities.Count; i++)
            {
                string thisNodeNameFunc = "CATHODE::" + entities[i].className + "::";
                string weirdCathodeNaming = "";
                for (int z = 0; z < entities[i].className.Length; z++)
                {
                    if (char.IsUpper(entities[i].className[z]) && z != 0)
                    {
                        weirdCathodeNaming += "_";
                    }
                    weirdCathodeNaming += entities[i].className[z].ToString().ToUpper();
                }
                string thisNodeNameFuncAlt = "CATHODE::" + weirdCathodeNaming + "::";

                for (int x = 0; x < functions.Count; x++)
                {
                    if ((functions[x].functionName.Length >= thisNodeNameFunc.Length && functions[x].functionName.Substring(0, thisNodeNameFunc.Length) == thisNodeNameFunc)// ||
                        /*(functions[x].functionName.Length >= thisNodeNameFuncAlt.Length && functions[x].functionName.Substring(0, thisNodeNameFuncAlt.Length) == thisNodeNameFuncAlt)*/)
                    {
                        for (int z = 0; z < entities[i].nodeParameters.Count; z++)
                        {
                            string[] fnSplit = functions[x].functionNameStripped.Split(new char[] { '_' }, 2);
                            string fnSplitConc = (fnSplit.Length == 1) ? fnSplit[0] : fnSplit[1];
                            if (functions[x].functionNameStripped.ToUpper() == entities[i].nodeParameters[z].parameterName.Replace(' ', '_').ToUpper() ||
                                fnSplitConc.ToUpper() == entities[i].nodeParameters[z].parameterName.Replace(' ', '_').ToUpper() ||
                                functions[x].functionNameStripped.ToUpper() == entities[i].nodeParameters[z].parameterVariableNameStripped.Substring(2).ToUpper() ||
                                fnSplitConc.ToUpper() == entities[i].nodeParameters[z].parameterVariableNameStripped.Substring(2).ToUpper())
                            {
                                //This stops us matching constructors
                                if (functions[x].functionNameStripped == entities[i].className) continue;

                                if (entities[i].nodeParameters[z].parameterDataType != "")
                                {
                                    Console.WriteLine("ERROR: '" + entities[i].nodeParameters[z].parameterName + "' on '" + entities[i].nodeName + "' has already been assigned DataType!");
                                }

                                string compiledContent = "";
                                for (int p = 0; p < functions[x].functionContent.Count; p++)
                                {
                                    compiledContent += functions[x].functionContent[p].Trim() + " ";
                                }
                                string[] splitByInterface = compiledContent.Split(new[] { "EntityInterface::" }, StringSplitOptions.None);
                                if (splitByInterface.Length > 2)
                                {
                                    Console.WriteLine("More than one EntityInterface call in: " + functions[x].functionName);
                                }
                                else if (splitByInterface.Length < 2)
                                {
                                    string[] splitBySetEnt = compiledContent.Split(new[] { "set_entity_of_type_" }, StringSplitOptions.None);
                                    if (splitBySetEnt.Length > 2)
                                    {
                                        Console.WriteLine("More than one set_entity_of_type_ call in: " + functions[x].functionName);
                                    }
                                    else if (splitBySetEnt.Length < 2)
                                    {
                                        string[] splitByFilterRes = compiledContent.Split(new[] { "filter_resources_from_entities_" }, StringSplitOptions.None);
                                        if (splitByFilterRes.Length > 2)
                                        {
                                            Console.WriteLine("More than one filter_resources_from_entities_ call in: " + functions[x].functionName);
                                        }
                                        else if (splitByFilterRes.Length < 2)
                                        {
                                            Console.WriteLine("NO filter_resources_from_entities_ call in: " + functions[x].functionName);
                                            debug_out.Add("");
                                            debug_out.Add("");
                                            debug_out.Add("######");
                                            debug_out.Add(functions[x].functionName);
                                            debug_out.Add("######");
                                            debug_out.Add("");
                                            debug_out.AddRange(functions[x].functionContent);
                                        }
                                        else
                                        {
                                            entities[i].nodeParameters[z].parameterDataType = splitByFilterRes[1].Split('(')[0];
                                            entities[i].nodeParameters[z].parameterDataType = entities[i].nodeParameters[z].parameterDataType.Substring(0, entities[i].nodeParameters[z].parameterDataType.Length - 1);
                                        }
                                    }
                                    else
                                    {
                                        entities[i].nodeParameters[z].parameterDataType = splitBySetEnt[1].Split('(')[0];
                                        entities[i].nodeParameters[z].parameterDataType = entities[i].nodeParameters[z].parameterDataType.Substring(0, entities[i].nodeParameters[z].parameterDataType.Length - 1);
                                    }
                                }
                                else
                                {
                                    string[] splitFunc = splitByInterface[1].Split('(');
                                    entities[i].nodeParameters[z].parameterDataType = splitFunc[0];
                                    if (!entities[i].nodeParameters[z].parameterDataType.Contains("call_triggers"))
                                    {
                                        splitFunc = splitFunc[1].Split(new[] { ");" }, StringSplitOptions.None);
                                        string defaultVarName = splitFunc[0].Split('&')[0];
                                        entities[i].nodeParameters[z].parameterDefaultValue = compiledContent.Split(new[] { defaultVarName + " = " }, StringSplitOptions.None)[1].Split(';')[0];
                                        entities[i].nodeParameters[z].doesHaveDefaultValue = true;
                                        if (entities[i].nodeParameters[z].parameterDefaultValue.Contains("__Class::m_") ||
                                            entities[i].nodeParameters[z].parameterDefaultValue.Contains("__Class:: m_"))
                                        {
                                            if (entities[i].nodeParameters[z].parameterDefaultValue.Contains("__Class:: m_"))
                                                entities[i].nodeParameters[z].parameterDefaultValue = entities[i].nodeParameters[z].parameterDefaultValue.Replace("__Class:: m_", "__Class::m_");

                                            //hacky way of remembering we're an enum
                                            entities[i].nodeParameters[z].parameterDefaultValue = entities[i].nodeParameters[z].parameterDefaultValue.Split(new[] { "__Class::m_" }, StringSplitOptions.None)[0];
                                            string enumIndex = compiledContent.Split(new[] { defaultVarName + " = " }, StringSplitOptions.None)[2].Split(';')[0];
                                            if (enumIndex.Length >= 2 && enumIndex.Substring(0, 2) == "0x")
                                            {
                                                enumIndex = Int32.Parse(enumIndex.Substring(2), System.Globalization.NumberStyles.HexNumber).ToString();
                                            }
                                            entities[i].nodeParameters[z].parameterDefaultValue = entities[i].nodeParameters[z].parameterDefaultValue + " (" + enumIndex + ")";
                                        }
                                        string thing = "StringTable::offset_from_string(StringTable::m_instance,";
                                        if (entities[i].nodeParameters[z].parameterDefaultValue.Contains(thing))
                                        {
                                            //strings are looked up in table, so remove the call
                                            entities[i].nodeParameters[z].parameterDefaultValue = entities[i].nodeParameters[z].parameterDefaultValue.Substring(thing.Length + 1);
                                            entities[i].nodeParameters[z].parameterDefaultValue = entities[i].nodeParameters[z].parameterDefaultValue.Substring(0, entities[i].nodeParameters[z].parameterDefaultValue.Length - 2);
                                        }
                                        if (entities[i].nodeParameters[z].parameterDefaultValue == "0xffffffff")
                                        {
                                            //some default to 0xffffffff which the lookup returns "" for 
                                            entities[i].nodeParameters[z].parameterDefaultValue = "";
                                        }
                                        if (entities[i].nodeParameters[z].parameterDefaultValue == "(MemoryAllocationBase *)0x0")
                                        {
                                            //This is used for references, we can ignore
                                            entities[i].nodeParameters[z].parameterDefaultValue = "";
                                            entities[i].nodeParameters[z].doesHaveDefaultValue = false;
                                        }
                                        if (entities[i].nodeParameters[z].parameterDefaultValue.Length >= 2 && entities[i].nodeParameters[z].parameterDefaultValue.Substring(0, 2) == "0x")
                                        {
                                            if (entities[i].nodeParameters[z].parameterDataType == "find_parameter_int_ ")
                                            {
                                                entities[i].nodeParameters[z].parameterDefaultValue = Int32.Parse(entities[i].nodeParameters[z].parameterDefaultValue.Substring(2), System.Globalization.NumberStyles.HexNumber).ToString();
                                            }
                                            else
                                            {
                                                Console.WriteLine("Binning param " + entities[i].nodeParameters[z].parameterDefaultValue + " (datatype is " + entities[i].nodeParameters[z].parameterDataType + ")");
                                                //TODO: figure these out
                                                entities[i].nodeParameters[z].parameterDefaultValue = "";
                                                entities[i].nodeParameters[z].doesHaveDefaultValue = false;
                                            }
                                        }
                                        if (entities[i].nodeParameters[z].parameterDefaultValue.Length >= 4 && entities[i].nodeParameters[z].parameterDefaultValue.Substring(0, 4) == "DAT_")
                                        {
                                            Console.WriteLine("Binning param " + entities[i].nodeParameters[z].parameterDefaultValue + " (datatype is " + entities[i].nodeParameters[z].parameterDataType + ")");
                                            //TODO: figure these out
                                            entities[i].nodeParameters[z].parameterDefaultValue = "";
                                            entities[i].nodeParameters[z].doesHaveDefaultValue = false;
                                        }
                                        if (entities[i].nodeParameters[z].parameterDefaultValue == "default_Enum")
                                        {
                                            //TODO: figure these out
                                            entities[i].nodeParameters[z].parameterDefaultValue = "";
                                            entities[i].nodeParameters[z].doesHaveDefaultValue = false;
                                        }
                                        for (int ll = 0; ll < defaultVariables.Count; ll++)
                                        {
                                            if (entities[i].nodeParameters[z].parameterDefaultValue == defaultVariables[ll].VarToMatch)
                                            {
                                                entities[i].nodeParameters[z].parameterDefaultValue = defaultVariables[ll].Value;
                                                break;
                                            }
                                        }
                                    }
                                }
                                //tidy
                                if (entities[i].nodeParameters[z].parameterDataType != "")
                                {
                                    string dt = entities[i].nodeParameters[z].parameterDataType;
                                    dt = dt.Trim();
                                    dt = dt.Replace("__", "::");
                                    while (dt[dt.Length - 1] == '_' ||
                                           dt[dt.Length - 1] == ':')
                                    {
                                        dt = dt.Substring(0, dt.Length - 1);
                                    }
                                    string st1 = "find_parameter_";
                                    if (dt.Length >= st1.Length && dt.Substring(0, st1.Length) == st1)
                                    {
                                        dt = dt.Substring(st1.Length);
                                    }
                                    string st2 = "find_resource_";
                                    if (dt.Length >= st2.Length && dt.Substring(0, st2.Length) == st2)
                                    {
                                        dt = dt.Substring(st2.Length);
                                        dt = "[resource] " + dt;
                                    }
                                    if (dt == "call_triggers")
                                    {
                                        dt = ""; //call_triggers is a function call to trigger the target's trigger
                                    }
                                    dt = dt.Replace("CATHODE::MemoryPtr_", "CATHODE::MemoryPtr -> ");
                                    dt = dt.Replace("CATHODE::ArrayPtr_", "CATHODE::ArrayPtr -> ");
                                    entities[i].nodeParameters[z].parameterDataType = dt;
                                }
                                entities[i].nodeParameters[z].didFindFunction = true;
                            }
                        }
                    }
                }
            }
            File.WriteAllLines("DEBUG_outfromparse.txt", debug_out);
            #endregion

            #region TEST: Output all datatypes
            List<string> allDataTypes = new List<string>();
            List<string> allDataTypes2 = new List<string>();
            for (int i = 0; i < entities.Count; i++)
            {
                for (int x = 0; x < entities[i].nodeParameters.Count; x++)
                {
                    if (!allDataTypes.Contains(entities[i].nodeParameters[x].parameterDataType)) allDataTypes.Add(entities[i].nodeParameters[x].parameterDataType);
                    if (!allDataTypes2.Contains(entities[i].nodeParameters[x].parameterDataType_FROMFIRSTDUMP)) allDataTypes2.Add(entities[i].nodeParameters[x].parameterDataType_FROMFIRSTDUMP);
                }
            }
            allDataTypes.Sort();
            allDataTypes2.Sort();
            File.WriteAllLines("DEBUG_AllDataTypesNew.txt", allDataTypes);
            File.WriteAllLines("DEBUG_AllDataTypesNew_firstparse.txt", allDataTypes2);
            #endregion

            #region TEST: Output all datatypes 2
            List<string> allValues = new List<string>();
            for (int i = 0; i < entities.Count; i++)
            {
                for (int x = 0; x < entities[i].nodeParameters.Count; x++)
                {
                    if (entities[i].nodeParameters[x].doesHaveDefaultValue)
                    {
                        allValues.Add(entities[i].nodeParameters[x].parameterDataType);
                    }
                }
            }
            allValues.Sort();
            File.WriteAllLines("DEBUG_allvalues.txt", allValues);
        #endregion

        #region TODO: What did this do?
        /*
        List<string> allDataTypes = new List<string>();
        string entityParamLookupFunc = "EntityInterface::find_parameter_";
        string entityResourceLookupFunc = "EntityInterface::find_resource_";
        bool shouldBeChecking = false;
        string activeParamName = "";
        for (int x = 0; x < nodes.Count; x++)
        {
            string thisNodeNameFunc = "CATHODE::" + nodes[x].nodeName + "::get_";
            for (int p = 0; p < 2; p++)
            {
                shouldBeChecking = false;
                for (int i = 0; i < cDump.Count; i++)
                {
                    if (cDump[i].Length <= thisNodeNameFunc.Length) continue;
                    if (cDump[i].Substring(0, 2) == "//") continue;
                    if (cDump[i].Contains(thisNodeNameFunc))
                    {
                        //hack fix for function calls, not definitions
                        bool isValid = false;
                        for (int z = 0; z < 4; z++)
                        {
                            if (cDump[i + z] == "{")
                            {
                                isValid = true;
                                break;
                            }
                        }
                        if (!isValid) continue;

                        string[] funcContent = cDump[i].Split(new[] { thisNodeNameFunc }, StringSplitOptions.None);
                        string thisParamName = funcContent[funcContent.Length - 1].Split('(')[0];
                        if (thisParamName == "implementation_size") continue; //This isn't a node param, but a cathode helper function
                        activeParamName = thisParamName;
                        shouldBeChecking = true;
                        continue;
                    }
                    if (!shouldBeChecking) continue;
                    if (cDump[i] == "}")
                    {
                        Console.WriteLine("Stopping search for " + activeParamName);
                        shouldBeChecking = false;
                        continue;
                    }
                    if (cDump[i].Length <= entityParamLookupFunc.Length) continue;
                    if (cDump[i].Contains(entityParamLookupFunc))
                    {
                        string[] datatypeContent = cDump[i].Split(new[] { entityParamLookupFunc }, StringSplitOptions.None);
                        string thisDataTypeName = datatypeContent[datatypeContent.Length - 1].Split('(')[0];
                        if (thisDataTypeName[thisDataTypeName.Length - 1] == '_') thisDataTypeName = thisDataTypeName.Substring(0, thisDataTypeName.Length - 1);
                        thisDataTypeName = thisDataTypeName.Replace('_', ':');
                        CathodeParameter thisParam = nodes[x].nodeParameters.FirstOrDefault(o => o.parameterName.ToUpper() == activeParamName.ToUpper());
                        if (thisParam == null)
                        {
                            thisParam = new CathodeParameter();
                            thisParam.parameterName = activeParamName;
                            nodes[x].nodeParametersFromGet.Add(thisParam);
                        }
                        if (thisParam.parameterDataType != "")
                        {
                            Console.WriteLine("Updating data type for parameter '" + thisParam.parameterName + "' on node '" + nodes[x].nodeName + "'... was previously '" + thisParam.parameterDataType + "'");
                        }
                        thisParam.parameterDataType = thisDataTypeName;
                        if (!allDataTypes.Contains(thisDataTypeName)) allDataTypes.Add(thisDataTypeName);
                        shouldBeChecking = false;
                        continue;
                    }
                }
                thisNodeNameFunc = "CATHODE::" + nodes[x].nodeName + "::";
            }
        }
        allDataTypes.Sort();
        File.WriteAllLines("all_datatypes.txt", allDataTypes);
        */
        #endregion

        jump_to_dump:


            List<CathodeNode> interfaces = entities.FindAll(o => o.nodeName.Contains("Interface") && o.nodeName != "SmokeCylinderAttachmentInterface");
            interfaces = interfaces.OrderBy(o => o.className).ToList();
            entities.RemoveAll(o => o.nodeName.Contains("Interface") && o.nodeName != "SmokeCylinderAttachmentInterface");
            entities = entities.OrderBy(o => o.className).ToList();
            entities.InsertRange(0, interfaces);

            #region Dump "entities_and_interfaces.html"
            {
                List<string> output_html = new List<string>();
                bool did_divider = false;
                output_html.Add("<h1>Interfaces</h1>");
                for (int i = 0; i < entities.Count; i++)
                {
                    if (!entities[i].nodeName.Contains("Interface") && !did_divider)
                    {
                        did_divider = true;
                        output_html.Add("<br><br><h1>Entities</h1>");
                    }


                    output_html.Add("<hr>");
                    output_html.Add("<a name=\"" + entities[i].className + "\"></a>");
                    output_html.Add("<h3 title=\"" + entities[i].className + "\">" + entities[i].nodeName + "</h3>");

                    if (interfaceMappings.ContainsKey(entities[i].className))
                    {
                        output_html.Add("<h5 style=\"line-height:0;\">Inherits from <a href=\"#" + interfaceMappings[entities[i].className] + "\">" + interfaceMappings[entities[i].className] + "</a></h5>");
                    }
                    else
                    {
                        Console.WriteLine(entities[i].className + " does not inherit?");
                    }

                    output_html.Add("<ul>");
                    for (int x = 0; x < entities[i].nodeParameters.Count; x++)
                    {
                        string toAdd = " title='" + entities[i].nodeParameters[x].parameterVariableNameStripped + "'> [" + entities[i].nodeParameters[x].parameterVariableType + "] ";
                        if (entities[i].nodeParameters[x].parameterDataType_FROMFIRSTDUMP == "")
                        {
                            toAdd = "<li style='color:" + ((entities[i].nodeParameters[x].didFindFunction) ? "orange" : "red") + ";'" + toAdd;
                            toAdd += entities[i].nodeParameters[x].parameterName;
                        }
                        else
                        {
                            toAdd = "<li style='color:green;'" + toAdd;
                            toAdd += entities[i].nodeParameters[x].parameterName + " [DataType: " + entities[i].nodeParameters[x].parameterDataType_FROMFIRSTDUMP + "]";
                        }
                        if (entities[i].nodeParameters[x].parameterDefaultValue != "")
                        {
                            toAdd += " [DefaultVal: " + entities[i].nodeParameters[x].parameterDefaultValue + "]";
                        }
                        output_html.Add(toAdd + "</li>");
                    }
                    for (int x = 0; x < entities[i].nodeParametersFromGet.Count; x++)
                    {
                        output_html.Add("<li style='color:brown;'>" + entities[i].nodeParametersFromGet[x].parameterName + " [DataType: " + entities[i].nodeParametersFromGet[x].parameterDataType_FROMFIRSTDUMP + "]</li>");
                    }
                    if (customMethodMappings.ContainsKey(entities[i].className))
                    {
                        for (int x = 0; x < customMethodMappings[entities[i].className].Count; x++)
                        {
                            output_html.Add("<li style='color:purple;'>" + customMethodMappings[entities[i].className][x] + "</li>");
                        }
                    }
                    output_html.Add("</ul>");
                }
                File.WriteAllLines("entities_and_interfaces.html", output_html);
            }
            #endregion

            #region Dump "index.html"
            {
                CathodeNode EntityMethodInterface = entities.FirstOrDefault(o => o.className == "EntityMethodInterface");
                Dictionary<string, List<CathodeParameter>> EMI_Lookup = new Dictionary<string, List<CathodeParameter>>();
                for (int i = 0; i < EntityMethodInterface.nodeParameters.Count; i++)
                {
                    if (EntityMethodInterface.nodeParameters[i].parameterVariableType == "method")
                    {
                        List<CathodeParameter> param_links = new List<CathodeParameter>();
                        for (int x = 0; x < 3; x++)
                        {
                            param_links.Add(EntityMethodInterface.nodeParameters[i + x]);
                        }
                        EMI_Lookup.Add(EntityMethodInterface.nodeParameters[i].parameterName, param_links);
                    }
                }

                List<string> output_html = new List<string>();
                bool did_divider = false;
                output_html.Add("<div class=\"docs-wrapper\"><div id=\"docs-sidebar\" class=\"docs-sidebar\"><nav id=\"docs-nav\" class=\"docs-nav navbar\"><ul class=\"section-items list-unstyled nav flex-column pb-3\">");
                output_html.Add("<li class=\"nav-item section-title\"><a class=\"nav-link scrollto\" href=\"#interfaces\"><span class=\"theme-icon-holder me-2\"><i class=\"fas fa-code\"></i></span>Interfaces</a></li>");
                for (int i = 0; i < entities.Count; i++)
                {
                    if (entities[i].nodeName == "EntityMethodInterface") continue;
                    if (!entities[i].nodeName.Contains("Interface") && !did_divider)
                    {
                        did_divider = true;
                        output_html.Add("<li class=\"nav-item section-title mt-3\"><a class=\"nav-link scrollto\" href=\"#entities\"><span class=\"theme-icon-holder me-2\"><i class=\"fas fa-code\"></i></span>Entities</a></li>");
                    }
                    output_html.Add("<li class=\"nav-item\"><a class=\"nav-link scrollto\" href=\"#" + entities[i].className + "\">" + entities[i].className + "</a></li>");
                }
                output_html.Add("</ul></nav></div>");
                did_divider = false;
                output_html.Add("<div class=\"docs-content\"><div class=\"container\">");
                output_html.Add("<article class=\"docs-article\" id=\"interfaces\"><header class=\"docs-header\"><h1 class=\"docs-heading\">Interfaces <span class=\"docs-time\">Last updated: " + DateTime.Now.ToString() + "</span></h1></header>");
                for (int i = 0; i < entities.Count; i++)
                {
                    if (!entities[i].nodeName.Contains("Interface") && !did_divider)
                    {
                        did_divider = true;
                        output_html.Add("</article><article class=\"docs-article\" id=\"entities\"><header class=\"docs-header\"><h1 class=\"docs-heading\">Entities <span class=\"docs-time\">Last updated: " + DateTime.Now.ToString() + "</span></h1></header>");
                    }
                    if (entities[i].nodeName == "EntityMethodInterface") continue;

                    output_html.Add("<section class=\"docs-section\" id=\"" + entities[i].className + "\">");
                    output_html.Add("<h2 class=\"section-heading\">" + entities[i].className + "</h3>");

                    if (interfaceMappings.ContainsKey(entities[i].className))
                    {
                        if (interfaceMappings[entities[i].className] != "EntityMethodInterface")
                            output_html.Add("<p>Inherits from <a class=\"scrollto\" href=\"#" + interfaceMappings[entities[i].className] + "\"><strong>" + interfaceMappings[entities[i].className] + "</strong></a></p>");
                    }

                    Dictionary<string, List<CathodeParameter>> parameters = new Dictionary<string, List<CathodeParameter>>();
                    for (int x = 0; x < entities[i].nodeParameters.Count; x++)
                    {
                        string type = entities[i].nodeParameters[x].parameterVariableType;
                        if (!parameters.ContainsKey(type)) parameters.Add(type, new List<CathodeParameter>());
                        parameters[type].Add(entities[i].nodeParameters[x]);
                    }
                    
                    foreach (KeyValuePair<string, List<CathodeParameter>> vals in parameters)
                    {
                        output_html.Add("<h5>" + vals.Key.First().ToString().ToUpper() + vals.Key.Substring(1).ToLower() + "s:</h5>");
                        output_html.Add("<ul>");
                        foreach (CathodeParameter param in vals.Value)
                        {
                            string toAdd = "<li title='" + param.parameterVariableNameStripped + "'>";
                            if (param.parameterDataType_FROMFIRSTDUMP == "")
                            {
                                toAdd += param.parameterName;
                            }
                            else
                            {
                                toAdd += param.parameterName + " [DataType: " + param.parameterDataType_FROMFIRSTDUMP + "]";
                            }
                            if (param.parameterDefaultValue != "")
                            {
                                toAdd += " [DefaultVal: " + param.parameterDefaultValue + "]";
                            }
                            output_html.Add(toAdd + "</li>");
                        }
                        output_html.Add("</ul>");
                    }

                    if (customMethodMappings.ContainsKey(entities[i].className))
                    {
                        output_html.Add("<h5>Methods:</h5>");
                        for (int x = 0; x < customMethodMappings[entities[i].className].Count; x++)
                        {
                            string val = "<li>" + customMethodMappings[entities[i].className][x];
                            if (EMI_Lookup.ContainsKey(customMethodMappings[entities[i].className][x]))
                            {
                                CathodeParameter relay = EMI_Lookup[customMethodMappings[entities[i].className][x]].FirstOrDefault(o => o.parameterVariableType == "relay");
                                if (relay != null) val += " -> " + relay.parameterName;
                            }
                            output_html.Add(val + "</li>");
                        }
                    }
                    output_html.Add("</ul>");
                    output_html.Add("</section>");
                }
                output_html.Add("</article>");

                List<string> final_html = new List<string>();

                List<string> template_html = File.ReadAllLines("../../../../docs/cathode-entities/index_template.html").ToList();
                for (int i = 0; i < template_html.Count; i++)
                {
                    if (template_html[i] == "<!--CONTENT_GOES_HERE-->")
                    {
                        final_html.AddRange(output_html);
                    }
                    else
                    {
                        final_html.Add(template_html[i]);
                    }
                }

                File.WriteAllLines("../../../../docs/cathode-entities/index.html", final_html);
            }
            #endregion

            #region Dump "entities.json"
            {
                JObject obj = JObject.Parse("{}");
                JArray obj_nodes = new JArray();
                for (int i = 0; i < entities.Count; i++)
                {
                    JObject obj_node = new JObject();
                    obj_node["name"] = entities[i].nodeName;
                    obj_node["class"] = entities[i].className;
                    JArray obj_node_params = new JArray();
                    for (int x = 0; x < entities[i].nodeParameters.Count; x++)
                    {
                        JObject obj_node_param = new JObject();
                        obj_node_param["name"] = entities[i].nodeParameters[x].parameterName;
                        obj_node_param["variable"] = entities[i].nodeParameters[x].parameterVariableNameStripped;
                        obj_node_param["usage"] = entities[i].nodeParameters[x].parameterVariableType;
                        //if (nodes[i].nodeParameters[x].didFindFunction)
                        //{
                        obj_node_param["datatype"] = entities[i].nodeParameters[x].parameterDataType_FROMFIRSTDUMP;
                        if (entities[i].nodeParameters[x].doesHaveDefaultValue)
                        {
                            switch (entities[i].nodeParameters[x].parameterDataType)
                            {
                                case "bool":
                                    if (entities[i].nodeParameters[x].parameterDefaultValue.ToUpper() == "TRUE") obj_node_param["value"] = true;
                                    else if (entities[i].nodeParameters[x].parameterDefaultValue.ToUpper() == "FALSE") obj_node_param["value"] = false;
                                    else Console.WriteLine("Failed to match TRUE/FALSE for bool: " + entities[i].nodeParameters[x].parameterName + " - " + entities[i].nodeName);
                                    break;
                                case "float":
                                    obj_node_param["value"] = Convert.ToSingle(entities[i].nodeParameters[x].parameterDefaultValue);
                                    break;
                                case "int":
                                    obj_node_param["value"] = Convert.ToInt32(entities[i].nodeParameters[x].parameterDefaultValue);
                                    break;
                                case "CATHODE::String":
                                    obj_node_param["value"] = entities[i].nodeParameters[x].parameterDefaultValue;
                                    break;
                                case "CATHODE::Enum":
                                    string[] enumStringSplit = entities[i].nodeParameters[x].parameterDefaultValue.Split(' ');
                                    if (enumStringSplit.Length != 2) Console.WriteLine("Enum split should be 2! " + entities[i].nodeParameters[x].parameterName + " - " + entities[i].nodeName);
                                    int index = Convert.ToInt32(enumStringSplit[1].Substring(1, enumStringSplit[1].Length - 2));
                                    obj_node_param["value"] = index;
                                    obj_node_param["value_enumname"] = enumStringSplit[0];
                                    break;
                                    //All other types currently unsupported by C parser
                            }
                        }
                        //}
                        obj_node_params.Add(obj_node_param);
                    }
                    obj_node["parameters"] = obj_node_params;
                    obj_nodes.Add(obj_node);
                }
                obj["nodes"] = obj_nodes;
                File.WriteAllText("entities.json", obj.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            #endregion

            #region Dump "cathode_entities.bin"
            {
                CathodeNode EntityMethodInterface = entities.FirstOrDefault(o => o.className == "EntityMethodInterface");
                BinaryWriter bw = new BinaryWriter(File.OpenWrite("cathode_entities.bin"));
                bw.BaseStream.SetLength(0);
                bw.Write(entities.Count);
                for (int i = 0; i < entities.Count; i++)
                {
                    List<string> sanity = new List<string>();

                    bw.Write(entities[i].nodeName);
                    bw.Write(entities[i].className);
                    long posToReturnTo = bw.BaseStream.Position;
                    bw.Write(0);

                    string node = entities[i].className;
                    while (node != "" && node != "EntityResourceInterface" && node != "EntityMethodInterface")
                    {
                        CathodeNode nodeObj = entities.FirstOrDefault(o => o.className == node);
                        for (int x = 0; x < nodeObj.nodeParameters.Count; x++)
                        {
                            if (sanity.Contains(nodeObj.nodeParameters[x].parameterName)) continue;
                            sanity.Add(nodeObj.nodeParameters[x].parameterName);

                            bw.Write(nodeObj.nodeParameters[x].parameterName);
                            bw.Write(nodeObj.nodeParameters[x].parameterVariableNameStripped);
                            bw.Write(nodeObj.nodeParameters[x].parameterVariableType);
                            bw.Write(nodeObj.nodeParameters[x].parameterDataType_FROMFIRSTDUMP);
                            //TODO: write default value
                        }
                        if (customMethodMappings.ContainsKey(node))
                        {
                            for (int x = 0; x < customMethodMappings[node].Count; x++)
                            {
                                CathodeParameter param = EntityMethodInterface.nodeParameters.FirstOrDefault(o => o.parameterName == customMethodMappings[node][x]);
                                if (sanity.Contains(param.parameterName)) continue;
                                sanity.Add(param.parameterName);

                                bw.Write(param.parameterName);
                                bw.Write(param.parameterVariableNameStripped);
                                bw.Write(param.parameterVariableType);
                                bw.Write(param.parameterDataType_FROMFIRSTDUMP);
                                //TODO: should we also include the relay, etc for each one of these?
                            }
                        }

                        if (interfaceMappings.ContainsKey(node))
                            node = interfaceMappings[node];
                        else
                            node = "";
                    }

                    long thisPos = bw.BaseStream.Position;
                    bw.BaseStream.Position = posToReturnTo;
                    bw.Write(sanity.Count);
                    bw.BaseStream.Position = thisPos;
                }
                bw.Close();
            }
            #endregion

            #region Dump "entity_names.txt"
            List<string> nodeNames = new List<string>();
            for (int i = 0; i < entities.Count; i++)
                nodeNames.Add("\"" + entities[i].nodeName + "\",");
            nodeNames.Sort();
            File.WriteAllLines("entity_names.txt", nodeNames);
            #endregion

            #region Dump "entities.md"
            List<string> newDumpForGithub = new List<string>();
            entities = entities.OrderBy(o => o.nodeName).ToList();
            for (int i = 0; i < entities.Count; i++)
            {
                newDumpForGithub.Add("## " + entities[i].nodeName);
                for (int x = 0; x < entities[i].nodeParameters.Count; x++)
                {
                    newDumpForGithub.Add(" * " + entities[i].nodeParameters[x].parameterName);
                }
                newDumpForGithub.Add("");
            }
            File.WriteAllLines("entities.md", newDumpForGithub);
            #endregion

            #region Dump "interface_inheritance.cs"
            List<string> interface_switch = new List<string>();
            interface_switch.Add("switch () {");
            for (int i = 0; i < entities.Count; i++)
            {
                interface_switch.Add("case FunctionType." + entities[i].className + ":");
                if (interfaceMappings.ContainsKey(entities[i].className))
                {
                    interface_switch.Add("return FunctionType." + interfaceMappings[entities[i].className] + ";");
                }
                else
                {
                    interface_switch.Add("throw new Exception();");
                }
            }
            interface_switch.Add("}");
            File.WriteAllLines("interface_inheritance.cs", interface_switch);
            #endregion

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
