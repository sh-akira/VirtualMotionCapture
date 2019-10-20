:abc: Polyglot
---
[![Gem Version](https://badge.fury.io/rb/jekyll-polyglot.svg)](https://badge.fury.io/rb/jekyll-polyglot)
[![Build Status](https://travis-ci.org/untra/polyglot.svg?branch=master)](https://travis-ci.org/untra/polyglot)

__Polyglot__ is a fast, painless, open-source internationalization plugin for [Jekyll](http://jekyllrb.com) blogs. Polyglot is easy to setup and use with any Jekyll project, and it scales to the languages you want to support. With fallback support for missing content, automatic url relativization, and powerful SEO tools, Polyglot allows any multi-language jekyll blog to focus on content without the cruft.

## Why?
Jekyll doesn't provide native support for multi-language blogs. This plugin was modeled after the [jekyll-multiple-languages-plugin](https://github.com/screeninteraction/jekyll-multiple-languages-plugin), whose implementation I liked, but execution I didn't.

## Installation
Add jekyll-polyglot to your `Gemfile` if you are using Bundler:
```Ruby
group :jekyll_plugins do
   gem "jekyll-polyglot"
end
```

Or install the gem manually by doing `gem install jekyll-polyglot` and specify the plugin using `_config.yml`:
```YAML
plugins:
  - jekyll-polyglot
```

## Configuration
In your `_config.yml` file, add the following preferences
```YAML
languages: ["en", "sv", "de", "fr"]
default_lang: "en"
exclude_from_localization: ["javascript", "images", "css"]
parallel_localization: true
```
These configuration preferences indicate
- what i18n languages you wish to support
- what is your default "fallback" language for your content
- what root level folders are you excluding from localization
- whether to run language processing in parallel or serial

## How To Use It
When adding new posts and pages, add to the YAML front matter:
```
lang: sv
```
or whatever appropriate [I18n language code](https://developer.chrome.com/webstore/i18n)
the page should build for. And you're done. Ideally, when designing your site, you should
organize files by their relative urls.

Polyglot works by associating documents with similar permalinks to the `lang` specified in their frontmatter. Files that correspond to similar routes should have identical permalinks. If you don't provide a permalink for a post, ___make sure you are consistent___ with how you place and name corresponding files:
```
_posts/2010-03-01-salad-recipes-en.md
_posts/2010-03-01-salad-recipes-sv.md
_posts/2010-03-01-salad-recipes-fr.md
```

Organized names will generate consistent permalinks when the post is rendered, and polyglot will know to build seperate language versions of
the website using only the files with the correct `lang` variable in the front matter.

In short:
* Be consistent with how you name and place your *posts* files
* Always give your *pages* permalinks in the frontmatter
* Don't overthink it, :wink:


#### Fallback Language Support
Lets say you are building your website. You have an `/about/` page written in *english*, *german* and
*swedish*. You are also supporting a *french* website, but you never designed a *french* version of your `/about/` page!

No worries. Polyglot ensures the sitemap of your *english* site matches your *french* site, matches your *swedish* and *german* sites too. In this case, because you specified a `default_lang` variable in your `_config.yml`, all sites missing their languages' counterparts will fallback to your `default_lang`, so content is preserved across different languages of your site.

#### Relativized Local Urls
No need to meticulously manage anchor tags to link to your correct language. Polyglot modifies how pages get written to the site so your *french* links keep vistors on your *french* blog.
```md
---
title: au sujet de notre entreprise
permalink: /about/
lang: fr
---
Nous sommes un restaurant situé à Paris . [Ceci est notre menu.](/menu/)
```
becomes
```html
<header class="post-header">
  <h1 class="post-title">au sujet de notre entreprise</h1>
</header>

<article class="post-content">
  <p>Nous sommes un restaurant situé à Paris . <a href="/fr/menu/">Ceci est notre menu.</a></p>
</article>
```
Notice the link `<a href="/fr/menu/">...` directs to the french website.

Even if you are falling back to `default_lang` page, relative links built on the *french* site will
still link to *french* pages.

#### Relativized Absolute Urls
If you defined a site `url` in your `_config.yaml`, polyglot will automatically relativize absolute links pointing to your website directory:

```md
---
lang: fr
---
Cliquez [ici]({{site.url}}) pour aller à l'entrée du site.
```
becomes
```html
<p>Cliquez <a href="https://mywebsite.com/fr/">ici</a> pour aller à l'entrée du site.
```

#### Disabling Url Relativizing
If you don't want a url to be relativized, you can add a space explicitly into the href to prevents a url from being relativized by polyglot.

For example, the following urls will be relativized:

```html
href="http://mywebsite.com/about"
href="/about"
```

and the following urls will be left alone:

```html
href=" http://mywebsite.com/about"
href=" /about"
```

combine with a [html minifier](https://github.com/digitalsparky/jekyll-minifier) for a polished and production ready website.

#### Localized site.data

There are cases when `site.data` localization is required.
For instance: you might need to localize `_data/navigation.yml` that holds "navigation menu".
In order to localize it, just place language-specific files in `_data/:lang/...` folder, and Polyglot will bring those keys to the top level.

## How It Works
This plugin makes modifications to existing Jekyll classes and modules, namely `Jekyll::StaticFile` and `Jekyll::Site`. These changes are as lightweight and slim as possible. The biggest change is in `Jekyll::Site.process`. Polyglot overwrites this method to instead spawn a separate thread for each language you intend to process the site for. Each of those threads calls the original `Jekyll::Site.process` method with its language in mind, ensuring your website scales to support any number of languages, while building all of your site languages simultaneously.

`Jekyll::Site.process` is the entry point for the Jekyll build process. Take care whatever other plugins you use do not also attempt to overwrite this method. You may have problems.

## Features
This plugin stands out from other I18n Jekyll plugins.
- automatically corrects your relative links, keeping your *french* visitors on your *french* website, even when content has to fallback to the `default_lang`.
- builds all versions of your website *simultaneously*, allowing big websites to scale efficiently.
- provides the liquid tag `{{ site.languages }}` to get an array of your I18n strings.
- provides the liquid tag `{{ site.default_lang }}` to get the default_lang I18n string.
- provides the liquid tag `{{ site.active_lang }}` to get the I18n language string the website was built for. Alternative names for `active_lang` can be configured via `config.lang_vars`.
- provides the liquid tag `{{ I18n_Headers https://yourwebsite.com/ }}` to append SEO bonuses to your website.
- provides `site.data` localization for efficient rich text replacement.
- a creator that will answer all of your questions and issues.

## SEO Recipes
Jekyll-polyglot has a few spectacular [Search Engine Optimization techniques](https://untra.github.io/polyglot/seo) to ensure your Jekyll blog gets the most out of it's multilingual audience. Check them out!

### Other Websites Built with Polyglot
let us know if you make a multilingual blog you want to share:
* [Polyglot example project website](http://polyglot.untra.io)
* [LogRhythm Corporate Website](http://logrhythm.com)
* [All Over Earth](https://allover.earth/)

## Compatibility
Currently supports Jekyll 3.0 .
Windows users will need to disable parallel_localization on their machines by setting `parallel_localization: false` in the `_config.yml`

## Copyright
Copyright (c) Samuel Volin 2017. License: MIT
