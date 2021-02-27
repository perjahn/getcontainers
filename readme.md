This is a cli tool for visualizing versions of containers for multiple
kubernetes clusters/environments. Using the -e parameter to specify
environments, should be a substring of cluster names, particular useful
when each environment is using a multicluster setup.

Example command line:

    dotnet run -- -e dev,test,qa,prod

Typical output:

    Container   dev    test   qa     prod
    container1  1.3.0  1.2.0  1.2.0  1.1.0
    container2  4.5.0  4.5.0  4.4.0  4.1.0

As the this util currently works, it requires a label ”version”
on pods (apparently this is a convention according to istio's best
practice). It would of course be possible to retrieve and/or
calculate each container version from somewhere else, like the
hash of images or making it more dynamic, or whatever...

Because the official k8s library is used, cluster credentials from
~/.kube/config are used. I.e. you must first login to every cluster
you intend to query, an easy verification is that tools like kubectl
must work. Adding credentials is usually done using a cloud specific
cli tool, like gcloud cli for gcp.
