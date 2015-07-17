﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Inklewriter.Runtime
{
    [JsonObject(MemberSerialization.OptIn)]
	public /* TODO: abstract */ class Object
	{
		public Runtime.Object parent { get; set; }

        public Runtime.DebugMetadata debugMetadata { 
            get {
                if (_debugMetadata == null) {
                    if (parent) {
                        return parent.debugMetadata;
                    }
                }

                return _debugMetadata;
            }

            set {
                _debugMetadata = value;
            }
        }

        // TODO: Come up with some clever solution for not having
        // to have debug metadata on the object itself?!
//        #if DEBUG
//        [JsonProperty("dm")]
//        #endif
        DebugMetadata _debugMetadata;

        // Serialised type
        [JsonProperty("%t")]
        protected virtual string serialisedTypeName {
            get {
                var customNameAttr = (CustomJsonNameAttribute) Attribute.GetCustomAttribute (GetType(), typeof(CustomJsonNameAttribute));
                if (customNameAttr != null)
                    return customNameAttr.name;

                // Default: unqualified name (i.e. without namespace)
                return this.GetType ().Name;
            }
        }

        public int? DebugLineNumberOfPath(Path path)
        {
            if (path == null)
                return null;
            
            // Try to get a line number from debug metadata
            var root = this.rootContentContainer;
            if (root) {
                var targetContent = root.ContentAtPath (path);
                if (targetContent) {
                    var dm = targetContent.debugMetadata;
                    if (dm != null) {
                        return dm.startLineNumber;
                    }
                }
            }

            return null;
        }

		public Path path 
		{ 
			get 
			{
				if (parent == null) {
					return new Path ();
				} else {

                    // Maintain a Stack so that the order of the components
                    // is reversed when they're added to the Path.
                    // We're iterating up the hierarchy from the leaves/children to the root.
                    var comps = new Stack<Path.Component> ();

                    var child = this;
                    Container container = child.parent as Container;

                    while (container) {

                        var namedChild = child as INamedContent;
                        if (namedChild != null && namedChild.hasValidName) {
                            comps.Push (new Path.Component (namedChild.name));
                        } else {
                            comps.Push (new Path.Component (container.content.IndexOf(child)));
                        }

                        child = container;
                        container = container.parent as Container;
                    }

                    return new Path (comps);
				}
			}
		}

        public Container rootContentContainer
        {
            get 
            {
                Runtime.Object ancestor = this;
                while (ancestor.parent) {
                    ancestor = ancestor.parent;
                }
                return ancestor as Container;
            }
        }

		public Object ()
		{
		}

        protected void SetChild<T>(ref T obj, T value) where T : Runtime.Object
        {
            if (obj)
                obj.parent = null;

            obj = value;

            if( obj )
                obj.parent = this;
        }
            
        // Allow implicit conversion to bool so you don't have to do:
        // if( myObj != null ) ...
        public static implicit operator bool (Object obj)
        {
            var isNull = object.ReferenceEquals (obj, null);
            return !isNull;
        }

        public static bool operator ==(Object a, Object b)
        {
            return object.ReferenceEquals (a, b);
        }

        public static bool operator !=(Object a, Object b)
        {
            return !(a == b);
        }

        public override bool Equals (object obj)
        {
            return object.ReferenceEquals (obj, this);
        }

        public override int GetHashCode ()
        {
            return base.GetHashCode ();
        }
	}
}

