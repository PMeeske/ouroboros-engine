using Ouroboros.Providers.Kubernetes;

namespace Ouroboros.Tests.Kubernetes;

[Trait("Category", "Unit")]
public sealed class KubernetesModelsTests
{
    [Fact]
    public void PodInfo_SetsProperties()
    {
        var pod = new KubernetesPodInfo
        {
            Name = "nginx-pod",
            Namespace = "default",
            Phase = "Running"
        };

        pod.Name.Should().Be("nginx-pod");
        pod.Namespace.Should().Be("default");
        pod.Phase.Should().Be("Running");
    }

    [Fact]
    public void PodInfo_DefaultCollections()
    {
        var pod = new KubernetesPodInfo { Name = "p", Namespace = "n", Phase = "r" };
        pod.Labels.Should().BeEmpty();
        pod.Containers.Should().BeEmpty();
        pod.RestartCount.Should().Be(0);
    }

    [Fact]
    public void DeploymentInfo_SetsProperties()
    {
        var deploy = new KubernetesDeploymentInfo
        {
            Name = "web",
            Namespace = "prod",
            Replicas = 3,
            ReadyReplicas = 3,
            AvailableReplicas = 3
        };

        deploy.Replicas.Should().Be(3);
        deploy.ReadyReplicas.Should().Be(3);
    }

    [Fact]
    public void ServiceInfo_SetsProperties()
    {
        var svc = new KubernetesServiceInfo
        {
            Name = "api",
            Namespace = "default",
            Type = "ClusterIP"
        };

        svc.Type.Should().Be("ClusterIP");
        svc.Ports.Should().BeEmpty();
        svc.Selector.Should().BeEmpty();
    }

    [Fact]
    public void PortInfo_Defaults()
    {
        var port = new KubernetesPortInfo { Port = 80, TargetPort = 8080 };

        port.Protocol.Should().Be("TCP");
        port.NodePort.Should().BeNull();
    }
}
