[![Build](https://github.com/perjahn/getcontainers/actions/workflows/build.yml/badge.svg)](https://github.com/perjahn/getcontainers/actions/workflows/build.yml)
[![CodeQL](https://github.com/perjahn/getcontainers/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/perjahn/getcontainers/actions/workflows/github-code-scanning/codeql)

This is a cli tool for visualizing versions of containers for multiple
kubernetes clusters/environments. Specify a comma separated list of
environments, should be a substrings of cluster names, particular useful
when each environment is using a multicluster setup.

Example command line:

    dotnet run -- dev,test,uat,prod

Typical output:

    Container   dev    test   uat    prod
    container1  1.3.0  1.2.0  1.2.0  1.1.0
    container2  4.5.0  4.5.0  4.4.0  4.1.0

As default this tool is extracting each image version, but it is also
possible to use the label ”version” of pods, which apparently is a
convention according to istio's best practice. It would of course have
been possible to retrieve and/or calculate each version from somewhere
else, like the hash of images or making it more dynamic, or whatever...

This tool is no longer using the bloated official k8s library, but
cluster credentials are still read from `~/.kube/config`. Anyway you
must first login to every cluster you intend to query, i.e. verify that
tools like kubectl first are working. Adding credentials is usually
done by using a cloud specific cli tool, like gcloud cli for gcp.
