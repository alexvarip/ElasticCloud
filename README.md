# Elastic Cloud
### Elastic Resource Management in Cloud Computing

The purpose of the project is familiarization with cloud computing environments that offer IaaS (Infrastructure as a Service) services.
The main incentive is the use of the elasticity that the cloud provides through dynamic computing resources management.

For this purpose, an elastic web service will be created with the use of virtual machines from a public cloud computing service (okeanos.grnet.gr).
A cluster/web farm of web servers will be installed and configured that will distribute content to users connected to them.

The traffic that will result from the visits of users will change over time. As a result, the computing needs of the cluster will change.
Therefore a mechanism (load balancer) needs to be implemented, which depending on the observed workload it will dynamically change the reserved cloud resources.
