using PuppeteerSharp;

namespace REBUSS.GitDaif.Service.API.Agents.Helpers
{
    public class DomInspector
    {
        public async Task PrintDOM(IPage page)
        {
            var cdpPage = page.Client;
            var documentNode = await cdpPage.SendAsync<DomGetDocumentResponse>("DOM.getDocument");

            var domNodes = await cdpPage.SendAsync<DomQuerySelectorAllResponse>("DOM.querySelectorAll", new DomQuerySelectorAllRequest
            {
                NodeId = documentNode.Root.NodeId,
                Selector = "*"
            });

            Console.WriteLine("Elementy DOM:");
            foreach (var nodeId in domNodes.NodeIds)
            {
                var node = await cdpPage.SendAsync<DomDescribeNodeResponse>("DOM.describeNode", new DomDescribeNodeRequest
                {
                    NodeId = nodeId
                });

                var nodeName = node.Node.NodeName;
                var nodeIdAttr = node.Node.Attributes.Contains("id") ? node.Node.Attributes[Array.IndexOf(node.Node.Attributes, "id") + 1] : "null";
                var nodeClassAttr = node.Node.Attributes.Contains("class") ? node.Node.Attributes[Array.IndexOf(node.Node.Attributes, "class") + 1] : "null";
                var nodeRoleAttr = node.Node.Attributes.Contains("role") ? node.Node.Attributes[Array.IndexOf(node.Node.Attributes, "role") + 1] : "null";

                Console.WriteLine($"Type: {nodeName}, ID: {nodeIdAttr}, Class: {nodeClassAttr}, Role: {nodeRoleAttr}");
            }
        }
    }

    public class DomGetDocumentResponse
    {
        public DomNode Root { get; set; }
    }

    public class DomNode
    {
        public int NodeId { get; set; }
        public string NodeName { get; set; }
        public string[] Attributes { get; set; }
    }

    public class DomQuerySelectorAllRequest
    {
        public int NodeId { get; set; }
        public string Selector { get; set; }
    }

    public class DomQuerySelectorAllResponse
    {
        public int[] NodeIds { get; set; }
    }

    public class DomDescribeNodeRequest
    {
        public int NodeId { get; set; }
    }

    public class DomDescribeNodeResponse
    {
        public DomNode Node { get; set; }
    }
}
