[![Build](https://github.com/perjahn/getcontainers/workflows/Build/badge.svg)](https://github.com/perjahn/getcontainers/actions?query=workflow%3A%22Build%22)

This is a cli tool for visualizing versions of containers for multiple
kubernetes clusters/environments. Specify a comma separated list of
environments, should be a substrings of cluster names, particular useful
when each environment is using a multicluster setup.

Example command line:

    dotnet run -- dev,test,qa,prod

Typical output:

    Container   dev    test   qa     prod
    container1  1.3.0  1.2.0  1.2.0  1.1.0
    container2  4.5.0  4.5.0  4.4.0  4.1.0

As default this tool is extracting each image version, but it is also
possible to use the label ”version” of pods, which apparently is a
convention according to istio's best practice. It would of course have
been possible to retrieve and/or calculate each version from somewhere
else, like the hash of images or making it more dynamic, or whatever...

Because the official k8s library is used, cluster credentials from
~/.kube/config are used. I.e. you must first login to every cluster
you intend to query, an easy verification is that tools like kubectl
must work. Adding credentials is usually done by using a cloud
specific cli tool, like gcloud cli for gcp.
