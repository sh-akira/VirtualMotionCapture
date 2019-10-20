# -*- encoding: utf-8 -*-
# stub: jekyll-polyglot 1.3.1 ruby lib

Gem::Specification.new do |s|
  s.name = "jekyll-polyglot".freeze
  s.version = "1.3.1"

  s.required_rubygems_version = Gem::Requirement.new(">= 0".freeze) if s.respond_to? :required_rubygems_version=
  s.require_paths = ["lib".freeze]
  s.authors = ["Samuel Volin".freeze]
  s.date = "2017-09-02"
  s.description = "Fast open source i18n plugin for Jekyll blogs.".freeze
  s.email = "untra.sam@gmail.com".freeze
  s.homepage = "http://untra.github.io/polyglot".freeze
  s.licenses = ["MIT".freeze]
  s.rubygems_version = "2.7.6".freeze
  s.summary = "I18n plugin for Jekyll Blogs".freeze

  s.installed_by_version = "2.7.6" if s.respond_to? :installed_by_version

  if s.respond_to? :specification_version then
    s.specification_version = 4

    if Gem::Version.new(Gem::VERSION) >= Gem::Version.new('1.2.0') then
      s.add_runtime_dependency(%q<jekyll>.freeze, [">= 3.0"])
    else
      s.add_dependency(%q<jekyll>.freeze, [">= 3.0"])
    end
  else
    s.add_dependency(%q<jekyll>.freeze, [">= 3.0"])
  end
end
