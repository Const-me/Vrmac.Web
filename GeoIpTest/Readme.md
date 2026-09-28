# GeoIpTest

This project builds a simple test to verify correctness of the Vrmac.MaxMind / Vrmac.GeoIP data pipeline.

Specifically, it compiles a command-line tool which resolves a bunch of IP addresses
into broad geographic regions using both original and converted GeoIP databases,
compares the outcome, and throws exception on first mismatch.

The addresses being tested are 1M random IPv4 addresses,
and a small hardcoded list of IPv6 addresses.
The reason for the asymmetry, IPv6 address space is too large to use random number generator for meaningful tests of IPv6 lookups.